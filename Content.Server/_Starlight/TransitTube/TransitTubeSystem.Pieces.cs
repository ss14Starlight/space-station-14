using System.Linq;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Robust.Shared.Map;

namespace Content.Server._Starlight.TransitTube;

/// <summary>
/// Special tube pieces: speed-ups and brakes, block signals, taggers, request stops and waiting
/// in front of busy stations. Routing itself is shared, see <see cref="SharedTransitTubeSystem"/>.
/// </summary>
public sealed partial class TransitTubeSystem
{
    private static readonly TimeSpan _signalCheckInterval = TimeSpan.FromSeconds(0.25);

    private readonly HashSet<EntityUid> _section = [];
    private readonly HashSet<(EntityUid Tube, Direction Travel, string? Tag)> _sectionSeen = [];
    private readonly Queue<(Entity<DisposalTubeComponent> From, Direction Exit, int Depth, string? Tag)> _sectionFrontier = new();
    private readonly List<Direction> _sectionExits = [];
    private readonly HashSet<EntityUid> _waitChain = [];
    private readonly HashSet<Entity<ItemComponent>> _loadItems = [];
    private readonly HashSet<Entity<EntityStorageComponent>> _loadCrates = [];

    protected override void OnPodRouted(Entity<TransitTubeComponent> tube, Entity<DisposalHolderComponent> holder, Entity<TransitPodComponent> pod, Direction next)
    {
        var rotation = Transform(tube).LocalRotation;
        var travel = holder.Comp.CurrentDirection;

        // A tagger gives the pod a new destination rather than piling up old ones.
        if (HasComp<DisposalTaggerComponent>(tube))
            _holder.ClearTags(holder.AsNullable());

        if (TryComp<TransitTubeSpeedComponent>(tube, out var speed)
            && travel != Direction.Invalid
            && SameWay(travel, speed.Direction, rotation))
            ApplySpeed((tube, speed), pod, holder);

        if (TryComp<TransitTubeSensorComponent>(tube, out var sensor))
            _deviceLink.InvokePort(tube, GetPassengers(pod).Any() ? sensor.OccupiedPort : sensor.EmptyPort);

        // Out of the claimed section: other pods may have it.
        if (pod.Comp.ClaimedSignal is { } claimed
            && tube.Owner != claimed
            && !pod.Comp.ClaimedSection.Contains(tube.Owner))
            ReleaseSection(pod);

        if (next == Direction.Invalid)
            return;

        UpdatePodLevel(pod, tube);

        if (pod.Comp.Cargo)
            HandleCargo(tube, pod);

        EntityUid? nextTube = TryComp<DisposalTubeComponent>(tube, out var tubeComp)
            ? _disposalTube.GetTubeInDirection((tube, tubeComp), next)
            : null;

        // Off the planned route (turned back, bounced, a switch thrown, a tube on the way rebuilt): plan again from here.
        if (nextTube is { } replanFrom
            && travel != Direction.Invalid
            && pod.Comp.Target is { } target
            && target != tube.Owner
            && (!pod.Comp.Plan.TryGetValue((tube.Owner, travel), out var planned) || planned != next))
            Replan(pod, tube, (replanFrom, next), target);

        if (HoldsAt(tube, next, pod))
        {
            HoldAt(pod, holder, tube);
            return;
        }

        if (nextTube is not { } nextUid)
            return;

        // Stop a tube short of a red signal or a closed gate rather than on it, so the waiting pod
        // stays clear of the junction or crossing right behind it.
        if (HoldsAt(nextUid, next, pod))
        {
            HoldAt(pod, holder, nextUid);
            return;
        }

        // Let through by a signal: the section behind it is this pod's until it leaves.
        ClaimSection(pod, tube, next);
        ClaimSection(pod, nextUid, next);

        // Wait behind a pod standing on the line rather than ramming it.
        // One that would end up waiting on itself rides on, and the collision check turns it back.
        if (!pod.Comp.Boosted
            && StoppedPodAhead(pod, tube, nextUid, next) is { } stopped
            && !WaitWouldCycle(pod, stopped))
        {
            pod.Comp.WaitingBehind = stopped;
            Hold(pod, holder);
            return;
        }

        // Wait here, a tube short of a busy station the pod means to stop at, or of one it only
        // passes through while another pod stands in it. Pods going too fast to stop ram it instead.
        if (TryComp<TransitTubeStationComponent>(nextUid, out var station)
            && StationBlocked((nextUid, station), pod, next, out _))
        {
            pod.Comp.WaitingFor = nextUid;
            Hold(pod, holder);
        }
    }

    private bool StationBlocked(Entity<TransitTubeStationComponent> station, Entity<TransitPodComponent> pod, Direction travel, out bool stops)
    {
        stops = WouldStopAt(station, pod, travel);
        if (stops)
            return !CanDockAt(station, pod);

        return !pod.Comp.Boosted && station.Comp.DockedPod != null;
    }

    private bool HoldsAt(EntityUid uid, Direction direction, Entity<TransitPodComponent> pod)
    {
        if (GateClosed(uid))
            return true;

        return TryComp<TransitTubeBlockSignalComponent>(uid, out var signal)
            && SameWay(direction, signal.Direction, Transform(uid).LocalRotation)
            && !SignalClear((uid, signal), direction, pod);
    }

    private bool GateClosed(EntityUid uid)
        => HasComp<TransitTubeGateComponent>(uid) && IsEnabled(uid) && !IsEmagged(uid);

    private void HoldAt(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder, EntityUid at)
    {
        pod.Comp.HeldAtSignal = at;
        SetSignalLamp(at, true);
        Hold(pod, holder);
    }

    private void Replan(Entity<TransitPodComponent> pod, EntityUid origin, (EntityUid Tube, Direction Travel) start, EntityUid target)
    {
        if (TryFindRoute(origin, pod, uid => uid == target, out _, out _, start))
            return;

        CancelTrip(pod);
        PopupPassengers(pod, "transit-pod-no-route");
    }

    private void HandleCargo(Entity<TransitTubeComponent> tube, Entity<TransitPodComponent> pod)
    {
        var hasUnloader = TryComp<TransitTubeUnloaderComponent>(tube, out var unloader);
        var hasLoader = TryComp<TransitTubeLoaderComponent>(tube, out var loader);
        if ((!hasUnloader && !hasLoader) || !IsEnabled(tube.Owner) || !IsPowered(tube))
            return;

        var xform = Transform(tube);

        if (hasUnloader && GetPassengers(pod).Any())
        {
            EjectAll(pod, GetSide(xform, unloader!.Side));
            _audio.PlayPvs(tube.Comp.CargoSound, tube);
        }

        if (!hasLoader)
            return;

        _loadItems.Clear();
        _loadCrates.Clear();
        var side = GetSide(xform, loader!.Side);
        _lookup.GetEntitiesInRange(side, loader.Range, _loadItems);
        _lookup.GetEntitiesInRange(side, loader.Range, _loadCrates);

        var loaded = false;
        foreach (var item in _loadItems)
            loaded |= TryLoad(pod, item);

        foreach (var crate in _loadCrates)
            loaded |= TryLoad(pod, crate);

        if (loaded)
            _audio.PlayPvs(tube.Comp.CargoSound, tube);
    }

    private bool TryLoad(Entity<TransitPodComponent> pod, EntityUid uid)
    {
        // Loose things only: nothing bolted down, held, or already inside something.
        if (TerminatingOrDeleted(uid)
            || Transform(uid).Anchored
            || _container.IsEntityInContainer(uid))
            return false;

        return TryInsertPassenger(pod.AsNullable(), uid);
    }

    private static EntityCoordinates GetSide(TransformComponent xform, Direction side)
        => xform.Coordinates.Offset(new Angle(side.ToAngle() + xform.LocalRotation).GetDir().ToVec());

    private void ApplySpeed(Entity<TransitTubeSpeedComponent> tube, Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        // Brakes are plain mechanics; accelerators need power, and an emagged one overloads.
        if (tube.Comp.Speed is not { } boost)
        {
            SetSpeed(pod.AsNullable(), holder.AsNullable(), null);
            return;
        }

        if (!IsPowered(tube))
            return;

        SetSpeed(pod.AsNullable(), holder.AsNullable(), IsEmagged(tube) ? tube.Comp.OverloadSpeed : boost);
    }

    /// <summary>
    /// Sets the speed of a pod. Null puts it back to normal.
    /// </summary>
    public void SetSpeed(Entity<TransitPodComponent?> pod, Entity<DisposalHolderComponent?> holder, float? speed)
    {
        if (!Resolve(pod, ref pod.Comp) || !Resolve(holder, ref holder.Comp))
            return;

        var target = speed ?? pod.Comp.BaseSpeed;
        var boosted = target > pod.Comp.BaseSpeed + 0.01f;
        if (pod.Comp.Boosted != boosted)
        {
            pod.Comp.Boosted = boosted;
            Dirty(pod);
        }

        _holder.SetTraversalSpeed(holder.AsNullable(), target);
    }

    private void SetSignalLamp(EntityUid? signal, bool holding)
    {
        if (signal is { } uid && HasComp<TransitTubeBlockSignalComponent>(uid))
            _appearance.SetData(uid, TransitBlockSignalVisuals.Holding, holding);
    }

    private void LeaveSignal(Entity<TransitPodComponent> pod)
    {
        if (pod.Comp.HeldAtSignal is not { } signal)
            return;

        pod.Comp.HeldAtSignal = null;

        var query = EntityQueryEnumerator<TransitPodComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (uid != pod.Owner && other.HeldAtSignal == signal && !TerminatingOrDeleted(uid))
                return;
        }

        SetSignalLamp(signal, false);
    }

    private void Hold(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        pod.Comp.NextSignalCheck = _timing.CurTime + _signalCheckInterval;
        _holder.SetHalted(holder.AsNullable(), true);
    }

    private void UpdateHeld(Entity<TransitPodComponent> pod, DisposalHolderComponent holder, TimeSpan now)
    {
        UpdateBrake(pod, holder, now);

        if ((pod.Comp.HeldAtSignal == null && pod.Comp.WaitingFor == null && pod.Comp.WaitingBehind == null)
            || now < pod.Comp.NextSignalCheck)
            return;

        pod.Comp.NextSignalCheck = now + _signalCheckInterval;

        if (pod.Comp.WaitingBehind is { } ahead && StandsInTheWay(ahead))
            return;

        if (pod.Comp.HeldAtSignal is { } heldAt && Exists(heldAt) && HoldsAt(heldAt, holder.CurrentDirection, pod))
            return;

        if (pod.Comp.WaitingFor is { } stationUid && TryComp<TransitTubeStationComponent>(stationUid, out var station))
        {
            if (StationBlocked((stationUid, station), pod, holder.CurrentDirection, out var stops))
                return;

            // Claim the station, so only one of the pods waiting for it moves up.
            // A pod that only rides through claims nothing.
            if (stops)
                Reserve((stationUid, station), pod);
        }

        // Let through by the signal it waited at: claim the section now, before any other pod
        // checked in this same tick sees it clear too.
        if (pod.Comp.HeldAtSignal is { } releasedAt)
            ClaimSection(pod, releasedAt, holder.CurrentDirection);

        LeaveSignal(pod);
        pod.Comp.WaitingFor = null;
        pod.Comp.WaitingBehind = null;
        _holder.SetHalted((pod, holder), false);
    }

    /// <summary>
    /// Whether a block signal shows clear: it has power and no other pod is in the section ahead.
    /// </summary>
    private bool SignalClear(Entity<TransitTubeBlockSignalComponent> signal, Direction direction, Entity<TransitPodComponent> pod)
        // An unpowered signal shows red: nothing passes until power is back.
        => IsPowered(signal) && !SectionOccupied(signal, direction, pod);

    /// <summary>
    /// Whether another pod is in the section ahead of a block signal: every tube the pod would ride
    /// through up to the next signal or station, which is included.
    /// </summary>
    private bool SectionOccupied(Entity<TransitTubeBlockSignalComponent> signal, Direction direction, Entity<TransitPodComponent> pod)
    {
        if (!BuildSection(signal, direction, pod))
            return false;

        var query = EntityQueryEnumerator<TransitPodComponent, DisposalHolderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var otherPod, out var other, out var xform))
        {
            if (uid == pod.Owner)
                continue;

            // The tube a pod sits on covers docked and stalled pods too, the next tube covers pods about to enter.
            if ((other.NextTube is { } nextTubeUid && _section.Contains(nextTubeUid))
                || (TryGetTube(xform, out var under, other) && _section.Contains(under)))
                return true;

            // A pod let in by another signal holds its whole section until it is out, even before it gets here.
            if (otherPod.ClaimedSignal != null && otherPod.ClaimedSection.Overlaps(_section))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Claims the section behind a block signal for a pod it lets through, if the piece is one facing that way.
    /// </summary>
    private void ClaimSection(Entity<TransitPodComponent> pod, EntityUid signalUid, Direction direction)
    {
        if (pod.Comp.ClaimedSignal == signalUid
            || !TryComp<TransitTubeBlockSignalComponent>(signalUid, out var signal)
            || !SameWay(direction, signal.Direction, Transform(signalUid).LocalRotation))
            return;

        ReleaseSection(pod);
        if (!BuildSection((signalUid, signal), direction, pod))
            return;

        pod.Comp.ClaimedSignal = signalUid;
        pod.Comp.ClaimedSection.UnionWith(_section);
    }

    private static void ReleaseSection(Entity<TransitPodComponent> pod)
    {
        pod.Comp.ClaimedSignal = null;
        pod.Comp.ClaimedSection.Clear();
    }

    /// <summary>
    /// Fills <see cref="_section"/> with every tube a pod may ride through past a block signal, up to
    /// the next signal or station, which is included.
    /// </summary>
    /// <returns>False if the section is empty.</returns>
    private bool BuildSection(Entity<TransitTubeBlockSignalComponent> signal, Direction direction, Entity<TransitPodComponent> pod)
    {
        _section.Clear();

        if (!TryComp<DisposalTubeComponent>(signal, out var signalTube) || !TryComp<DisposalHolderComponent>(pod, out var holder))
            return false;

        // Follow every way this very pod may go: switches, routers, turntables and its plan included,
        // and both branches of a switch that picks at random.
        _sectionSeen.Clear();
        _sectionFrontier.Clear();
        if (direction != Direction.Invalid)
            _sectionFrontier.Enqueue(((signal, signalTube), direction, 0, null));

        while (_sectionFrontier.TryDequeue(out var step))
        {
            if (step.Depth >= signal.Comp.MaxSectionLength
                || _disposalTube.GetTubeInDirection(step.From, step.Exit) is not { } nextUid
                || !TryComp<DisposalTubeComponent>(nextUid, out var nextTube)
                || !_sectionSeen.Add((nextUid, step.Exit, step.Tag)))
                continue;

            _section.Add(nextUid);

            if (HasComp<TransitTubeStationComponent>(nextUid)
                || HasComp<TransitTubeBlockSignalComponent>(nextUid)
                || !TryComp<TransitTubeComponent>(nextUid, out var transit))
                continue;

            // A tagger on the way swaps the pod's tags, and the routers past it go by the new one.
            var tag = TryComp<DisposalTaggerComponent>(nextUid, out var tagger) ? tagger.Tag : step.Tag;

            PossibleRoutes((nextUid, transit), nextTube, (pod, holder), pod, step.Exit, _sectionExits, tag);
            foreach (var exit in _sectionExits)
                _sectionFrontier.Enqueue(((nextUid, nextTube), exit, step.Depth + 1, tag));
        }

        return _section.Count > 0;
    }

    /// <summary>
    /// A boosted pod slams into a dead end: everyone inside gets hurt, and so does the tube,
    /// until it breaks open.
    /// </summary>
    private void Impact(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        HurtPassengers(pod, pod.Comp.ImpactDamage, 1f);
        _audio.PlayPvs(pod.Comp.CollisionSound, pod);

        if (holder.Comp.NextTube is { } wall && Exists(wall))
            _damageable.ChangeDamage(wall, pod.Comp.ImpactTubeDamage);
    }

    private bool TryRequestStop(Entity<TransitTubeStationComponent> station, EntityUid user)
    {
        if (!station.Comp.RequestStop || station.Comp.DockedPod != null)
            return false;

        station.Comp.StopRequested = true;
        Dirty(station);
        _appearance.SetData(station, TransitStationVisuals.StopRequested, true);
        _popup.PopupEntity(Loc.GetString("transit-station-stop-requested"), station, user);
        return true;
    }

    private void RequestStopFromInside(Entity<TransitPodComponent> pod, EntityUid passenger)
    {
        var now = _timing.CurTime;
        if (!pod.Comp.StopRequested)
        {
            pod.Comp.StopRequested = true;
            pod.Comp.StopRequestedAt = now;
            Dirty(pod);
            _popup.PopupEntity(Loc.GetString("transit-pod-stop-requested"), passenger, passenger, PopupType.Medium);
            return;
        }

        // No station came up for a while, likely a loop without any: the emergency brake unlocks.
        if (now < pod.Comp.StopRequestedAt + pod.Comp.BrakeAvailableAfter || now < pod.Comp.NextBrake)
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-moving"), passenger, passenger);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, passenger, pod.Comp.BrakeDelay, new TransitPodBrakeDoAfterEvent(), pod, target: pod)
        {
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("transit-pod-brake-pulling"), passenger, passenger, PopupType.Medium);
    }

    [SubscribeLocalEvent]
    private void OnBrakeDoAfter(Entity<TransitPodComponent> ent, ref TransitPodBrakeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled
            || ent.Comp.State != TransitPodState.Moving
            || ent.Comp.Braked
            || !TryComp<DisposalHolderComponent>(ent, out var holder)
            || holder.Halted)
            return;

        args.Handled = true;

        // The pod stops right where it is, just long enough for everyone to climb out.
        ent.Comp.Braked = true;
        ent.Comp.BrakedUntil = _timing.CurTime + ent.Comp.BrakeDuration;
        _holder.SetHalted((ent, holder), true);
        _audio.PlayPvs(ent.Comp.CollisionSound, ent);
        PopupPassengers(ent, "transit-pod-braked");
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):user} pulled the emergency brake of {ToPrettyString(ent):pod}");
    }

    /// <summary>
    /// Lets a braked pod go once its time is up or nobody is left in it to climb out.
    /// </summary>
    private void UpdateBrake(Entity<TransitPodComponent> pod, DisposalHolderComponent holder, TimeSpan now)
    {
        if (!pod.Comp.Braked
            || (now < pod.Comp.BrakedUntil && GetPassengers(pod).Any(HasComp<MobStateComponent>)))
            return;

        pod.Comp.Braked = false;
        pod.Comp.NextBrake = now + pod.Comp.BrakeCooldown;
        _holder.SetHalted((pod, holder), false);
    }

    /// <summary>
    /// Whether a pod stands still for a while, and pods coming up behind it should wait rather than crash.
    /// </summary>
    private bool StandsInTheWay(EntityUid uid)
        => TryComp<TransitPodComponent>(uid, out var pod)
        && pod.State == TransitPodState.Moving
        && (pod.Braked || pod.WaitingBehind != null);

    private bool WaitWouldCycle(EntityUid pod, EntityUid ahead)
    {
        _waitChain.Clear();
        EntityUid? current = ahead;
        while (current is { } uid && _waitChain.Add(uid))
        {
            if (uid == pod)
                return true;

            current = TryComp<TransitPodComponent>(uid, out var other) ? other.WaitingBehind : null;
        }

        return false;
    }

    private void TurnBack(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder, TimeSpan now)
    {
        pod.Comp.NextCollision = now + _collisionCooldown;

        if (pod.Comp.Target != null || pod.Comp.AnyFree)
            CancelTrip(pod);

        ReleaseReservation(pod);
        PopupPassengers(pod, "transit-pod-turned-back");

        if (!_holder.Reverse(holder.AsNullable()))
            Stall(pod, holder);
    }

    /// <summary>
    /// A pod standing in the way on the tube a pod is about to enter, or further along the one it is on.
    /// </summary>
    private EntityUid? StoppedPodAhead(Entity<TransitPodComponent> pod, EntityUid tube, EntityUid nextTube, Direction next)
    {
        var position = _xform.GetWorldPosition(pod);
        var query = EntityQueryEnumerator<TransitPodComponent, DisposalHolderComponent>();
        while (query.MoveNext(out var uid, out var other, out var otherHolder))
        {
            if (uid == pod.Owner
                || other.State != TransitPodState.Moving
                || (!other.Braked && other.WaitingBehind == null))
                continue;

            if (otherHolder.CurrentTube == nextTube)
                return uid;

            if (otherHolder.CurrentTube == tube
                && System.Numerics.Vector2.Dot(_xform.GetWorldPosition(uid) - position, next.ToVec()) > 0)
                return uid;
        }

        return null;
    }
}
