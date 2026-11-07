using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Holder;
using Content.Shared.Disposal.Router;
using Content.Shared.Disposal.SignalRouter;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;

namespace Content.Shared._Starlight.TransitTube;

public abstract partial class SharedTransitTubeSystem : EntitySystem
{
    [Dependency] private DisposalTubeSystem _disposalTube = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedDisposalHolderSystem _holder = default!;

    // Runs before the upstream routers so they leave transit tubes to this handler.
    [SubscribeLocalEvent(before: [typeof(DisposalTubeSystem), typeof(DisposalRouterSystem), typeof(DisposalSignalRouterSystem)])]
    private void OnGetNextDirection(Entity<TransitTubeComponent> ent, ref GetDisposalsNextDirectionEvent args)
    {
        if (args.Handled || !TryComp<DisposalTubeComponent>(ent, out var tube))
            return;

        args.Handled = true;

        if (!TryComp<TransitPodComponent>(args.Holder, out var pod))
        {
            args.Next = PickExit(ent, tube, args.Holder.Comp.CurrentDirection);
            return;
        }

        args.Next = Route(ent, tube, args.Holder, pod);
        OnPodRouted(ent, args.Holder, (args.Holder.Owner, pod), args.Next);
    }

    private Direction Route(Entity<TransitTubeComponent> ent, DisposalTubeComponent tube, Entity<DisposalHolderComponent> holder, TransitPodComponent pod)
    {
        var travel = holder.Comp.CurrentDirection;
        var rotation = Transform(ent).LocalRotation;

        // One-way tubes turn back pods coming the wrong way.
        if (ent.Comp.OneWay is { } oneWay
            && travel != Direction.Invalid
            && !SameWay(travel, oneWay, rotation)
            && IsEnabled(ent))
            return travel.GetOpposite();

        if (TryComp<TransitTubeStationComponent>(ent, out var station) && !PassesStation(ent, tube, station, pod, travel))
        {
            // A pod reaching a station stops there; the server docks it when the holder halts.
            // Boosted pods never dock: a terminus is just a wall to them.
            pod.DockTarget = station.DockedPod == null && !pod.Boosted ? ent.Owner : null;
            return Direction.Invalid;
        }

        if (TryComp<TransitTubeTurntableComponent>(ent, out var turntable) && travel != Direction.Invalid)
        {
            var turned = new Angle(travel.ToAngle() + Angle.FromDegrees(turntable.Turn)).GetDir();
            return HasExit(ent, tube, turned) ? turned : Direction.Invalid;
        }

        if (TryComp<DisposalRouterComponent>(ent, out var router))
        {
            // Pods with a matching tag follow the arrow, the rest take the other way.
            var routerTags = router.Tags;
            var holderTags = holder.Comp.Tags;
            var matches = _holder.TagsOverlap(holder, routerTags)
                || (routerTags.Contains("*") && holderTags.Count > 0);

            return matches
                ? PickExit(ent, tube, travel, force: true)
                : PickExit(ent, tube, travel, avoid: Preferred(ent.Comp.PreferredExit, rotation));
        }

        if (TryComp<TransitTubeSwitchComponent>(ent, out var tubeSwitch))
        {
            var exit = tubeSwitch.Right ? tubeSwitch.RightExit : tubeSwitch.LeftExit;
            return PickExit(ent, tube, travel, preferred: Preferred(exit, rotation));
        }

        return PickExit(ent, tube, travel, force: ent.Comp.ForcePreferred);
    }

    /// <summary>
    /// Whether a pod goes through a station without stopping: boosted pods, and pods at a request stop
    /// nobody asked for. A pod with nowhere to go on stops anyway.
    /// </summary>
    private bool PassesStation(Entity<TransitTubeComponent> ent, DisposalTubeComponent tube, TransitTubeStationComponent station, TransitPodComponent pod, Direction travel)
    {
        if (travel == Direction.Invalid)
            return false;

        var passes = pod.Boosted
            || station.RequestStop && !station.StopRequested && !pod.StopRequested;

        return passes && PickExit(ent, tube, travel) != Direction.Invalid;
    }

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
    public Direction PickExit(Entity<TransitTubeComponent> ent, DisposalTubeComponent tube, Direction travel,
        Direction? preferred = null, bool force = false, Direction? avoid = null)
    {
        var exits = _disposalTube.GetTubeConnectableDirections((ent, tube));
        if (exits.Length == 0)
            return Direction.Invalid;

        if (travel == Direction.Invalid)
            return exits[0];

        preferred ??= Preferred(ent.Comp.PreferredExit, Transform(ent).LocalRotation);

        var back = travel.GetOpposite();
        var best = Direction.Invalid;
        var bestScore = double.MaxValue;
        var maxTurn = ent.Comp.MaxTurn + 1;

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
            if (force && exit == preferred)
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

    [SubscribeLocalEvent]
    private void OnSwitchInit(Entity<TransitTubeSwitchComponent> ent, ref ComponentInit args)
        => _deviceLink.EnsureSinkPorts(ent, ent.Comp.LeftPort, ent.Comp.RightPort, ent.Comp.TogglePort);

    [SubscribeLocalEvent]
    private void OnSwitchMapInit(Entity<TransitTubeSwitchComponent> ent, ref MapInitEvent args)
        => _appearance.SetData(ent, TransitSwitchVisuals.Right, ent.Comp.Right);

    [SubscribeLocalEvent]
    private void OnSwitchSignal(Entity<TransitTubeSwitchComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port == ent.Comp.TogglePort)
            SetSwitch(ent, !ent.Comp.Right);
        else if (args.Port == ent.Comp.LeftPort)
            SetSwitch(ent, false);
        else if (args.Port == ent.Comp.RightPort)
            SetSwitch(ent, true);
    }

    /// <summary>
    /// Whether a piece is switched on. Pieces that cannot be switched are always on.
    /// </summary>
    public bool IsEnabled(EntityUid uid)
        => !TryComp<TransitTubeToggleComponent>(uid, out var toggle) || toggle.Enabled;

    [SubscribeLocalEvent]
    private void OnToggleInit(Entity<TransitTubeToggleComponent> ent, ref ComponentInit args)
        => _deviceLink.EnsureSinkPorts(ent, ent.Comp.OnPort, ent.Comp.OffPort, ent.Comp.TogglePort);

    [SubscribeLocalEvent]
    private void OnToggleMapInit(Entity<TransitTubeToggleComponent> ent, ref MapInitEvent args)
        => _appearance.SetData(ent, TransitTubeToggleVisuals.Enabled, ent.Comp.Enabled);

    [SubscribeLocalEvent]
    private void OnToggleSignal(Entity<TransitTubeToggleComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port == ent.Comp.TogglePort)
            SetEnabled(ent, !ent.Comp.Enabled);
        else if (args.Port == ent.Comp.OnPort)
            SetEnabled(ent, true);
        else if (args.Port == ent.Comp.OffPort)
            SetEnabled(ent, false);
    }

    /// <summary>
    /// Switches a piece on or off.
    /// </summary>
    public void SetEnabled(Entity<TransitTubeToggleComponent> ent, bool enabled)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);
        _appearance.SetData(ent, TransitTubeToggleVisuals.Enabled, enabled);
    }

    /// <summary>
    /// Points a switch junction left or right.
    /// </summary>
    public void SetSwitch(Entity<TransitTubeSwitchComponent> ent, bool right)
    {
        if (ent.Comp.Right == right)
            return;

        ent.Comp.Right = right;
        Dirty(ent);
        _appearance.SetData(ent, TransitSwitchVisuals.Right, right);
    }

    [SubscribeLocalEvent]
    private void OnStationCanDrop(Entity<TransitTubeStationComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = HasComp<MobStateComponent>(args.Dragged) && ent.Comp.DockedPod != null;
        args.Handled = true;
    }
}
