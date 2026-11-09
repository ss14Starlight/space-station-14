using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Holder;
using Content.Shared.Disposal.Router;
using Content.Shared.Disposal.SignalRouter;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Content.Shared.DragDrop;
using Content.Shared.Emag.Systems;
using Content.Shared.Item;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;

namespace Content.Shared._Starlight.TransitTube;

public abstract partial class SharedTransitTubeSystem : EntitySystem
{
    [Dependency] private DisposalTubeSystem _disposalTube = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedDisposalHolderSystem _holder = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] protected IPrototypeManager _proto = default!;

    // Runs before the upstream routers so they leave transit tubes to this handler.
    [SubscribeLocalEvent(before: [typeof(DisposalTubeSystem), typeof(DisposalRouterSystem), typeof(DisposalSignalRouterSystem)])]
    private void OnGetNextDirection(Entity<TransitTubeComponent> ent, ref GetDisposalsNextDirectionEvent args)
    {
        if (args.Handled || !TryComp<DisposalTubeComponent>(ent, out var tube))
            return;

        args.Handled = true;
        var travel = args.Holder.Comp.CurrentDirection;

        if (!TryComp<TransitPodComponent>(args.Holder, out var pod))
        {
            args.Next = PickExit((ent, ent.Comp, tube), travel);
            return;
        }

        Entity<TransitPodComponent> podEnt = (args.Holder.Owner, pod);
        args.Next = Route(ent, tube, args.Holder, podEnt, travel);
        OnPodRouted(ent, args.Holder, podEnt, args.Next);
    }

    /// <summary>
    /// Where a pod entering this tube going <paramref name="travel"/> leaves it, or
    /// <see cref="Direction.Invalid"/> if it stops here.
    /// </summary>
    /// <param name="simulate">Only look, don't mark the pod for docking. Used to look ahead along the line.</param>
    /// <param name="tag">When looking ahead past a tagger: the only tag the pod will carry by then,
    /// empty for none. Null uses the tags it has now.</param>
    protected Direction Route(Entity<TransitTubeComponent> ent, DisposalTubeComponent tube, Entity<DisposalHolderComponent> holder,
        Entity<TransitPodComponent> pod, Direction travel, bool simulate = false, string? tag = null)
    {
        var rotation = Transform(ent).LocalRotation;
        TryComp<TransitTubeStationComponent>(ent, out var station);

        // A pod with a plan follows it to its target, past every other station and junction.
        if (station != null && ent.Owner == pod.Comp.Target)
            return StopAt((ent, station), pod, simulate);

        // A passenger asking to get out beats the trip: the pod stops at the first station on the way.
        if (station != null && travel != Direction.Invalid && StopsOnRequest(station, pod.Comp))
            return StopAt((ent, station), pod, simulate);

        if (travel != Direction.Invalid && pod.Comp.Plan.TryGetValue((ent.Owner, travel), out var planned))
            return planned;

        // One-way tubes turn back pods coming the wrong way, unless switched off or emagged.
        if (ent.Comp.OneWay is { } oneWay
            && travel != Direction.Invalid
            && !SameWay(travel, oneWay, rotation)
            && IsEnabled(ent.Owner)
            && !IsEmagged(ent))
            return travel.GetOpposite();

        if (station != null && !PassesStation(ent, tube, station, pod.Comp, travel))
            return StopAt((ent, station), pod, simulate);

        if (TryComp<TransitTubeTurntableComponent>(ent, out var turntable) && travel != Direction.Invalid)
            return Turn(ent, tube, turntable, travel);

        if (TryComp<DisposalRouterComponent>(ent, out var router))
        {
            // Pods with a matching tag follow the arrow, the rest take the other way.
            var routerTags = router.Tags;
            var holderTags = holder.Comp.Tags;
            var matches = tag != null
                ? tag.Length > 0 && (routerTags.Contains(tag) || routerTags.Contains("*"))
                : _holder.TagsOverlap(holder, routerTags) || (routerTags.Contains("*") && holderTags.Count > 0);

            return matches
                ? PickExit((ent, ent.Comp, tube), travel, force: true)
                : PickExit((ent, ent.Comp, tube), travel, avoid: Preferred(ent.Comp.PreferredExit, rotation));
        }

        if (TryComp<TransitTubeSwitchComponent>(ent, out var tubeSwitch))
        {
            // An emagged switch throws pods down whichever branch it likes.
            var right = IsEmagged(ent) ? _random.Prob(0.5f) : tubeSwitch.Right;
            return SwitchExit((ent, ent.Comp, tube), tubeSwitch, right, travel);
        }

        return PickExit((ent, ent.Comp, tube), travel, force: ForcesBranch(ent));
    }

    private Direction SwitchExit(Entity<TransitTubeComponent, DisposalTubeComponent> ent, TransitTubeSwitchComponent tubeSwitch, bool right, Direction travel)
    {
        var exit = right ? tubeSwitch.RightExit : tubeSwitch.LeftExit;
        return PickExit((ent, ent.Comp1, ent.Comp2), travel, preferred: Preferred(exit, Transform(ent).LocalRotation), force: true);
    }

    protected void PossibleRoutes(Entity<TransitTubeComponent> ent, DisposalTubeComponent tube, Entity<DisposalHolderComponent> holder,
        Entity<TransitPodComponent> pod, Direction travel, List<Direction> exits, string? tag = null)
    {
        exits.Clear();

        var planned = travel != Direction.Invalid && pod.Comp.Plan.ContainsKey((ent.Owner, travel));
        if (!planned && IsEmagged(ent))
        {
            if (TryComp<TransitTubeSwitchComponent>(ent, out var tubeSwitch) && !HasComp<DisposalRouterComponent>(ent))
            {
                AddExit(exits, SwitchExit((ent, ent.Comp, tube), tubeSwitch, true, travel));
                AddExit(exits, SwitchExit((ent, ent.Comp, tube), tubeSwitch, false, travel));
                return;
            }

            if (IsBranchSwitch(ent))
            {
                AddExit(exits, PickExit((ent, ent.Comp, tube), travel, force: true));
                AddExit(exits, PickExit((ent, ent.Comp, tube), travel));
                return;
            }
        }

        AddExit(exits, Route(ent, tube, holder, pod, travel, simulate: true, tag: tag));
    }

    private static void AddExit(List<Direction> exits, Direction exit)
    {
        if (exit != Direction.Invalid && !exits.Contains(exit))
            exits.Add(exit);
    }

    /// <summary>
    /// Whether a side branch sends pods down its arrow. A switchable one only does while it is on,
    /// and an emagged one picks at random.
    /// </summary>
    private bool ForcesBranch(Entity<TransitTubeComponent> ent)
    {
        if (!ent.Comp.ForcePreferred)
            return false;

        if (!HasComp<TransitTubeToggleComponent>(ent))
            return true;

        return IsEmagged(ent) ? _random.Prob(0.5f) : IsEnabled(ent.Owner);
    }

    /// <summary>
    /// Whether a piece is a side branch that can be switched on and off.
    /// </summary>
    protected bool IsBranchSwitch(Entity<TransitTubeComponent> ent)
        => ent.Comp.ForcePreferred && HasComp<TransitTubeToggleComponent>(ent);

    private Direction StopAt(Entity<TransitTubeStationComponent> station, Entity<TransitPodComponent> pod, bool simulate)
    {
        // A pod reaching a station stops there; the server docks it when the holder halts.
        // Boosted pods never dock: a terminus is just a wall to them.
        if (!simulate)
            pod.Comp.DockTarget = CanDockAt(station, pod) && !pod.Comp.Boosted ? station.Owner : null;

        return Direction.Invalid;
    }

    protected Direction Turn(Entity<TransitTubeComponent> ent, DisposalTubeComponent tube, TransitTubeTurntableComponent turntable, Direction travel)
    {
        var turned = new Angle(travel.ToAngle() + Angle.FromDegrees(turntable.Turn)).GetDir();
        return HasExit(ent, tube, turned) ? turned : Direction.Invalid;
    }

    /// <summary>
    /// Whether a pod can dock at a station now: nobody docked there and nobody else on the way to it.
    /// </summary>
    public bool CanDockAt(Entity<TransitTubeStationComponent> station, EntityUid pod)
        => station.Comp.DockedPod == null && (station.Comp.ReservedBy is not { } reserved || reserved == pod || !TryComp<TransitPodComponent>(reserved, out var other) || other.Reserved != station.Owner);

    /// <summary>
    /// Claims a station for a pod, so other pods treat it as busy until this one docks.
    /// </summary>
    public void Reserve(Entity<TransitTubeStationComponent> station, Entity<TransitPodComponent> pod)
    {
        station.Comp.ReservedBy = pod;
        pod.Comp.Reserved = station;
    }

    /// <summary>
    /// Whether a pod entering a station going <paramref name="travel"/> would stop there.
    /// </summary>
    protected bool WouldStopAt(Entity<TransitTubeStationComponent> station, Entity<TransitPodComponent> pod, Direction travel)
    {
        if (pod.Comp.Target == station.Owner || StopsOnRequest(station.Comp, pod.Comp))
            return true;

        if (pod.Comp.Plan.ContainsKey((station.Owner, travel))
            || !TryComp<TransitTubeComponent>(station, out var transit)
            || !TryComp<DisposalTubeComponent>(station, out var tube))
            return false;

        return !pod.Comp.Boosted && !PassesStation((station, transit), tube, station.Comp, pod.Comp, travel);
    }

    /// <summary>
    /// Whether a pod goes through a station without stopping: boosted pods, pods headed somewhere else,
    /// and pods at a request stop nobody asked for. A pod with nowhere to go on stops anyway.
    /// </summary>
    protected bool PassesStation(Entity<TransitTubeComponent> ent, DisposalTubeComponent tube, TransitTubeStationComponent station, TransitPodComponent pod, Direction travel)
    {
        if (travel == Direction.Invalid)
            return false;

        var passes = pod.Boosted
            || (pod.Target != null && pod.Target != ent.Owner)
            || (station.RequestStop && !station.StopRequested && !pod.StopRequested);

        return passes && PickExit((ent, ent.Comp, tube), travel) != Direction.Invalid;
    }

    private static bool StopsOnRequest(TransitTubeStationComponent station, TransitPodComponent pod)
        => pod.StopRequested && !pod.Boosted && !station.Depot;

    protected bool IsEmagged(EntityUid uid)
        => _emag.CheckFlag(uid, EmagType.Interaction);

    /// <summary>
    /// Whether a piece has power. Pieces that don't need power always do.
    /// </summary>
    public bool IsPowered(EntityUid uid)
        => _power.IsPowered(uid);

    /// <summary>
    /// Called on a pod after it picked its way through a tube.
    /// </summary>
    protected virtual void OnPodRouted(Entity<TransitTubeComponent> tube, Entity<DisposalHolderComponent> holder, Entity<TransitPodComponent> pod, Direction next)
    {
    }

    /// <summary>
    /// Whether a world direction points the same way as a tube-local direction.
    /// </summary>
    protected static bool SameWay(Direction world, Direction local, Angle rotation)
        => Math.Abs(Angle.ShortestDistance(world.ToAngle(), local.ToAngle() + rotation).Degrees) < 90;

    private static Direction? Preferred(Direction? local, Angle rotation)
        => local is { } dir ? new Angle(dir.ToAngle() + rotation).GetDir() : null;

    private bool HasExit(Entity<TransitTubeComponent> ent, DisposalTubeComponent tube, Direction exit)
        => Array.IndexOf(_disposalTube.GetTubeConnectableDirections((ent, tube)), exit) >= 0;

    /// <summary>
    /// Picks the straightest exit that does not lead back the way the pod came. Ties go to the
    /// preferred exit (the arrow on junctions), so a pod leaving the trunk of a junction always takes
    /// the same branch and pods coming back from either branch merge into the trunk.
    /// </summary>
    /// <param name="preferred">World direction to use instead of the tube's own preferred exit.</param>
    /// <param name="force">Take the preferred exit whenever it can be reached, not only on a tie.</param>
    /// <param name="avoid">World direction to take only if there is no other way on.</param>
    public Direction PickExit(Entity<TransitTubeComponent?, DisposalTubeComponent?> ent, Direction travel,
        Direction? preferred = null, bool force = false, Direction? avoid = null)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2))
            return Direction.Invalid;

        var exits = _disposalTube.GetTubeConnectableDirections((ent, ent.Comp2));
        if (exits.Length == 0)
            return Direction.Invalid;

        if (travel == Direction.Invalid)
            return exits[0];

        preferred ??= Preferred(ent.Comp1.PreferredExit, Transform(ent).LocalRotation);

        var back = travel.GetOpposite();
        var best = Direction.Invalid;
        var bestScore = double.MaxValue;
        var maxTurn = ent.Comp1.MaxTurn + 1;

        // A pod riding straight through can be forced off to the side, right angles included.
        // One coming down a diagonal branch has no straight way on, so it still merges into the trunk.
        var forceTurn = Array.IndexOf(exits, travel) >= 0 ? 91 : 46;

        foreach (var exit in exits)
        {
            if (exit == back)
                continue;

            var turn = Math.Abs(Angle.ShortestDistance(travel.ToAngle(), exit.ToAngle()).Degrees);
            if (turn > maxTurn)
                continue;

            // Lower is better: avoided exits go last, forced preferred exits first.
            var score = turn;
            if (exit == avoid)
                score += 1000;
            // Forcing only reaches branches ahead: a pod coming down one branch of a split
            // still merges into the trunk instead of crossing over to the other branch.
            if (force && exit == preferred && turn <= forceTurn)
                score -= 1000;

            var better = score < bestScore - 1
                || (Math.Abs(score - bestScore) <= 1 && exit == preferred);

            if (!better)
                continue;

            best = exit;
            bestScore = score;
        }

        return best;
    }

    /// <summary>
    /// Whether a pod takes this kind of thing: people for passenger pods, items and crates for cargo pods.
    /// </summary>
    public bool CanCarry(TransitPodComponent pod, EntityUid uid)
    {
        if (HasComp<MobStateComponent>(uid))
            return !pod.Cargo;

        return pod.Cargo && (HasComp<ItemComponent>(uid) || HasComp<EntityStorageComponent>(uid));
    }

    [SubscribeLocalEvent]
    private void OnGlassDamageModify(Entity<TransitTubeGlassComponent> ent, ref DamageModifyEvent args)
    {
        if (_proto.Resolve(ent.Comp.Material, out var material)
            && material.Modifiers is { } modifiers
            && _proto.Resolve(modifiers, out var set))
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, set);
    }

    [SubscribeLocalEvent]
    private void OnEmagged(Entity<TransitTubeComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || IsEmagged(ent))
            return;

        // Only pieces with something to break: switches, one-way tubes and accelerators.
        var speed = CompOrNull<TransitTubeSpeedComponent>(ent);
        if (!HasComp<TransitTubeSwitchComponent>(ent)
            && !IsBranchSwitch(ent)
            && ent.Comp.OneWay == null
            && speed?.Speed == null)
            return;

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnSwitchInit(Entity<TransitTubeSwitchComponent> ent, ref ComponentInit args)
        => _deviceLink.EnsureSinkPorts(ent, ent.Comp.LeftPort, ent.Comp.RightPort, ent.Comp.TogglePort);

    [SubscribeLocalEvent]
    private void OnSwitchMapInit(Entity<TransitTubeSwitchComponent> ent, ref MapInitEvent args)
        => _appearance.SetData(ent, TransitSwitchVisuals.Right, ent.Comp.Right);

    [SubscribeLocalEvent]
    private void OnSwitchSignal(Entity<TransitTubeSwitchComponent> ent, ref SignalReceivedEvent args)
    {
        // Without power the switch stays where it was.
        if (!IsPowered(ent))
            return;

        if (args.Port == ent.Comp.TogglePort)
            SetSwitch(ent.AsNullable(), !ent.Comp.Right);
        else if (args.Port == ent.Comp.LeftPort)
            SetSwitch(ent.AsNullable(), false);
        else if (args.Port == ent.Comp.RightPort)
            SetSwitch(ent.AsNullable(), true);
    }

    /// <summary>
    /// Whether a piece is switched on. Pieces that cannot be switched are always on.
    /// </summary>
    public bool IsEnabled(Entity<TransitTubeToggleComponent?> ent)
        => !Resolve(ent, ref ent.Comp, false) || ent.Comp.Enabled;

    [SubscribeLocalEvent]
    private void OnToggleInit(Entity<TransitTubeToggleComponent> ent, ref ComponentInit args)
        => _deviceLink.EnsureSinkPorts(ent, ent.Comp.OnPort, ent.Comp.OffPort, ent.Comp.TogglePort);

    [SubscribeLocalEvent]
    private void OnToggleMapInit(Entity<TransitTubeToggleComponent> ent, ref MapInitEvent args)
        => _appearance.SetData(ent, TransitTubeToggleVisuals.Enabled, ent.Comp.Enabled);

    [SubscribeLocalEvent]
    private void OnToggleSignal(Entity<TransitTubeToggleComponent> ent, ref SignalReceivedEvent args)
    {
        if (!IsPowered(ent))
            return;

        if (args.Port == ent.Comp.TogglePort)
            SetEnabled(ent.AsNullable(), !ent.Comp.Enabled);
        else if (args.Port == ent.Comp.OnPort)
            SetEnabled(ent.AsNullable(), true);
        else if (args.Port == ent.Comp.OffPort)
            SetEnabled(ent.AsNullable(), false);
    }

    /// <summary>
    /// Switches a piece on or off.
    /// </summary>
    public void SetEnabled(Entity<TransitTubeToggleComponent?> ent, bool enabled)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);
        _appearance.SetData(ent, TransitTubeToggleVisuals.Enabled, enabled);
    }

    /// <summary>
    /// Points a switch junction left or right.
    /// </summary>
    public void SetSwitch(Entity<TransitTubeSwitchComponent?> ent, bool right)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Right == right)
            return;

        ent.Comp.Right = right;
        Dirty(ent);
        _appearance.SetData(ent, TransitSwitchVisuals.Right, right);
    }

    [SubscribeLocalEvent]
    private void OnPodGetState(Entity<TransitPodComponent> ent, ref ComponentGetState args)
    {
        var plan = new List<TransitPlanStep>(ent.Comp.Plan.Count);
        foreach (var ((tube, travel), exit) in ent.Comp.Plan)
            plan.Add(new TransitPlanStep(GetNetEntity(tube), travel, exit));

        args.State = new TransitPodComponentState(ent.Comp.State, ent.Comp.Boosted, ent.Comp.StopRequested,
            GetNetEntity(ent.Comp.Target), ent.Comp.AnyFree, plan);
    }

    [SubscribeLocalEvent]
    private void OnPodHandleState(Entity<TransitPodComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not TransitPodComponentState state)
            return;

        ent.Comp.State = state.State;
        ent.Comp.Boosted = state.Boosted;
        ent.Comp.StopRequested = state.StopRequested;
        ent.Comp.Target = EnsureEntity<TransitPodComponent>(state.Target, ent);
        ent.Comp.AnyFree = state.AnyFree;

        ent.Comp.Plan.Clear();
        foreach (var step in state.Plan)
            ent.Comp.Plan[(EnsureEntity<TransitPodComponent>(step.Tube, ent), step.Travel)] = step.Exit;
    }

    [SubscribeLocalEvent]
    private void OnStationCanDrop(Entity<TransitTubeStationComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = ent.Comp.DockedPod is { } podUid && TryComp<TransitPodComponent>(podUid, out var pod) && CanCarry(pod, args.Dragged);
        args.Handled = true;
    }
}
