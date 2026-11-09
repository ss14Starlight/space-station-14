using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Content.Shared.Popups;

namespace Content.Server._Starlight.TransitTube;

/// <summary>
/// Special tube pieces: speed-ups and brakes, block signals, taggers, request stops and waiting
/// in front of busy stations. Routing itself is shared, see <see cref="SharedTransitTubeSystem"/>.
/// </summary>
public sealed partial class TransitTubeSystem
{
    private static readonly TimeSpan _signalCheckInterval = TimeSpan.FromSeconds(0.25);

    private readonly HashSet<EntityUid> _section = [];

    protected override void OnPodRouted(Entity<TransitTubeComponent> tube, Entity<DisposalHolderComponent> holder, Entity<TransitPodComponent> pod, Direction next)
    {
        var rotation = Transform(tube).LocalRotation;
        var travel = holder.Comp.CurrentDirection;

        // A tagger gives the pod a new destination rather than piling up old ones.
        if (HasComp<DisposalTaggerComponent>(tube))
            _holder.ClearTags(holder);

        if (TryComp<TransitTubeSpeedComponent>(tube, out var speed)
            && travel != Direction.Invalid
            && SameWay(travel, speed.Direction, rotation))
            ApplySpeed((tube, speed), pod, holder);

        if (next == Direction.Invalid)
            return;

        if (TryComp<TransitTubeBlockSignalComponent>(tube, out var signal)
            && SameWay(next, signal.Direction, rotation)
            && !SignalClear((tube, signal), next, pod))
        {
            pod.Comp.HeldAtSignal = tube;
            SetSignalLamp(tube, true);
            Hold(pod, holder);
            return;
        }

        if (!TryComp<DisposalTubeComponent>(tube, out var tubeComp)
            || _disposalTube.GetTubeInDirection((tube, tubeComp), next) is not { } nextUid)
            return;

        // Stop a tube short of a red signal rather than on it, so the waiting pod stays clear of
        // the junction or crossing right behind the signal.
        if (TryComp<TransitTubeBlockSignalComponent>(nextUid, out var ahead)
            && SameWay(next, ahead.Direction, Transform(nextUid).LocalRotation)
            && !SignalClear((nextUid, ahead), next, pod))
        {
            pod.Comp.HeldAtSignal = nextUid;
            SetSignalLamp(nextUid, true);
            Hold(pod, holder);
            return;
        }

        // Wait here, a tube short of a busy station the pod means to stop at.
        if (TryComp<TransitTubeStationComponent>(nextUid, out var station)
            && !CanDockAt((nextUid, station), pod)
            && WouldStopAt((nextUid, station), pod, next))
        {
            pod.Comp.WaitingFor = nextUid;
            Hold(pod, holder);
        }
    }

    private void ApplySpeed(Entity<TransitTubeSpeedComponent> tube, Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        // Brakes are plain mechanics; accelerators need power, and an emagged one overloads.
        if (tube.Comp.Speed is not { } boost)
        {
            SetSpeed(pod, holder, null);
            return;
        }

        if (!IsPowered(tube))
            return;

        SetSpeed(pod, holder, IsEmagged(tube) ? tube.Comp.OverloadSpeed : boost);
    }

    /// <summary>
    /// Sets the speed of a pod. Null puts it back to normal.
    /// </summary>
    public void SetSpeed(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder, float? speed)
    {
        var target = speed ?? pod.Comp.BaseSpeed;
        pod.Comp.Boosted = target > pod.Comp.BaseSpeed + 0.01f;
        _holder.SetTraversalSpeed(holder, target);
    }

    private void SetSignalLamp(EntityUid? signal, bool holding)
    {
        if (signal is { } uid && HasComp<TransitTubeBlockSignalComponent>(uid))
            _appearance.SetData(uid, TransitBlockSignalVisuals.Holding, holding);
    }

    private void Hold(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        pod.Comp.NextSignalCheck = _timing.CurTime + _signalCheckInterval;
        _holder.SetHalted(holder, true);
    }

    private void UpdateHeld(Entity<TransitPodComponent> pod, DisposalHolderComponent holder, TimeSpan now)
    {
        if (pod.Comp.HeldAtSignal == null && pod.Comp.WaitingFor == null || now < pod.Comp.NextSignalCheck)
            return;

        pod.Comp.NextSignalCheck = now + _signalCheckInterval;

        if (pod.Comp.HeldAtSignal is { } signalUid
            && TryComp<TransitTubeBlockSignalComponent>(signalUid, out var signal)
            && !SignalClear((signalUid, signal), holder.CurrentDirection, pod))
            return;

        if (pod.Comp.WaitingFor is { } stationUid && TryComp<TransitTubeStationComponent>(stationUid, out var station))
        {
            if (!CanDockAt((stationUid, station), pod))
                return;

            // Claim the station, so only one of the pods waiting for it moves up.
            Reserve((stationUid, station), pod);
        }

        SetSignalLamp(pod.Comp.HeldAtSignal, false);
        pod.Comp.HeldAtSignal = null;
        pod.Comp.WaitingFor = null;
        _holder.SetHalted((pod, holder), false);
    }

    /// <summary>
    /// Whether a block signal shows clear: it has power and no other pod is in the section ahead.
    /// </summary>
    private bool SignalClear(Entity<TransitTubeBlockSignalComponent> signal, Direction direction, Entity<TransitPodComponent> pod)
    {
        // An unpowered signal shows red: nothing passes until power is back.
        return IsPowered(signal) && !SectionOccupied(signal, direction, pod);
    }

    /// <summary>
    /// Whether another pod is in the section ahead of a block signal: every tube the pod would ride
    /// through up to the next signal or station, which is included.
    /// </summary>
    private bool SectionOccupied(Entity<TransitTubeBlockSignalComponent> signal, Direction direction, Entity<TransitPodComponent> pod)
    {
        _section.Clear();

        if (!TryComp<DisposalTubeComponent>(signal, out var signalTube) || !TryComp<DisposalHolderComponent>(pod, out var holder))
            return false;

        Entity<DisposalTubeComponent> current = (signal, signalTube);
        for (var i = 0; i < signal.Comp.MaxSectionLength && direction != Direction.Invalid; i++)
        {
            if (_disposalTube.GetTubeInDirection(current, direction) is not { } nextUid
                || !TryComp<DisposalTubeComponent>(nextUid, out var nextTube)
                || !_section.Add(nextUid))
                break;

            if (HasComp<TransitTubeStationComponent>(nextUid)
                || HasComp<TransitTubeBlockSignalComponent>(nextUid)
                || !TryComp<TransitTubeComponent>(nextUid, out var transit))
                break;

            // Follow the way this very pod would go: switches, routers, turntables and its plan included.
            direction = Route((nextUid, transit), nextTube, (pod, holder), pod, direction, simulate: true);
            current = (nextUid, nextTube);
        }

        if (_section.Count == 0)
            return false;

        var query = EntityQueryEnumerator<TransitPodComponent, DisposalHolderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var other, out var xform))
        {
            if (uid == pod.Owner)
                continue;

            // The tube a pod sits on covers docked and stalled pods too, the next tube covers pods about to enter.
            if (other.NextTube is { } nextTubeUid && _section.Contains(nextTubeUid)
                || TryGetTube(xform, out var under) && _section.Contains(under))
                return true;
        }

        return false;
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
        _appearance.SetData(station, TransitStationVisuals.StopRequested, true);
        _popup.PopupEntity(Loc.GetString("transit-station-stop-requested"), station, user);
        return true;
    }

    private void RequestStopFromInside(Entity<TransitPodComponent> pod, EntityUid passenger)
    {
        if (pod.Comp.StopRequested)
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-moving"), passenger, passenger);
            return;
        }

        pod.Comp.StopRequested = true;
        _popup.PopupEntity(Loc.GetString("transit-pod-stop-requested"), passenger, passenger, PopupType.Medium);
    }
}
