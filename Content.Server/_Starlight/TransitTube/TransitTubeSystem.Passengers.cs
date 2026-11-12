using System.Linq;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Unit;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Disposal.Tube;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.DragDrop;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Labels.Components;
using Content.Shared.RCD.Components;
using Robust.Shared.Map;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    [SubscribeLocalEvent]
    private void OnStationActivate(Entity<TransitTubeStationComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        // A request stop with no pod around also calls one from a depot.
        if (TryRequestStop(ent, args.User))
        {
            TryCallPod(ent, args.User);
            args.Handled = true;
            return;
        }

        args.Handled = TryUnload(ent, args.User) || TryLoadPulled(ent, args.User) || TryCallPod(ent, args.User) || TryBoard(ent, args.User, args.User);
    }

    [SubscribeLocalEvent]
    private void OnStationInteractUsing(Entity<TransitTubeStationComponent> ent, ref InteractUsingEvent args)
    {
        // Leave tools alone: an RPD still takes the station apart, a labeler still names it,
        // and so on. They can still be loaded by dragging them onto the station.
        if (args.Handled
            || IsStationTool(args.Used)
            || ent.Comp.DockedPod is not { } podUid
            || !TryComp<TransitPodComponent>(podUid, out var pod)
            || !pod.Cargo)
            return;

        args.Handled = true;

        if (!CanBoard(ent, args.User, popup: true) || !CheckPlatformSide(ent, args.User, args.User))
            return;

        // Load what is in hand straight into a cargo pod.
        if (_hands.TryDrop(args.User, args.Used) && TryInsertPassenger((podUid, pod), args.Used))
            UpdateDestinations(ent);
    }

    private bool IsStationTool(EntityUid used)
        => HasComp<RCDComponent>(used)
        || HasComp<HandLabelerComponent>(used)
        || HasComp<ToolComponent>(used)
        || HasComp<NetworkConfiguratorComponent>(used)
        || HasComp<EmagComponent>(used)
        || (TryComp<StackComponent>(used, out var stack) && TryGetGlassMaterial(stack, out _));

    /// <summary>
    /// Loads whatever the user pulls into a docked cargo pod. Crates can't be dragged,
    /// so they are pulled up to the station and loaded with a click.
    /// </summary>
    private bool TryLoadPulled(Entity<TransitTubeStationComponent> station, EntityUid user)
    {
        if (station.Comp.DockedPod is not { } podUid
            || !TryComp<TransitPodComponent>(podUid, out var pod)
            || !pod.Cargo
            || !TryComp<PullerComponent>(user, out var puller)
            || puller.Pulling is not { } pulled
            || !CanCarry(pod, pulled))
            return false;

        if (!CanBoard(station, user, popup: true) || !CheckPlatformSide(station, pulled, user))
            return true;

        if (TryInsertPassenger((podUid, pod), pulled))
            UpdateDestinations(station);

        return true;
    }

    /// <summary>
    /// Empties a docked cargo pod onto the platform.
    /// </summary>
    private bool TryUnload(Entity<TransitTubeStationComponent> station, EntityUid user)
    {
        if (station.Comp.DockedPod is not { } podUid
            || !TryComp<TransitPodComponent>(podUid, out var pod)
            || !pod.Cargo)
            return false;

        if (pod.State != TransitPodState.Open)
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-not-ready"), station, user);
            return true;
        }

        if (!GetPassengers((podUid, pod)).Any())
            return false;

        if (!_access.IsAllowed(user, station))
        {
            _popup.PopupEntity(Loc.GetString("transit-station-access-denied"), station, user);
            return true;
        }

        EjectAll((podUid, pod), GetPlatform(station));
        return true;
    }

    [SubscribeLocalEvent]
    private void OnStationDragDrop(Entity<TransitTubeStationComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryBoard(ent, args.Dragged, args.User);
    }

    private bool TryBoard(Entity<TransitTubeStationComponent> station, EntityUid passenger, EntityUid user)
    {
        if (station.Comp.DockedPod is { } podUid
            && TryComp<TransitPodComponent>(podUid, out var pod)
            && !CanCarry(pod, passenger))
        {
            if (passenger == user || HasComp<MobStateComponent>(passenger))
                _popup.PopupEntity(Loc.GetString(pod.Cargo ? "transit-pod-cargo-only" : "transit-pod-passengers-only"), station, user);
            return passenger == user;
        }

        if (station.Comp.DockedPod == null && !HasComp<MobStateComponent>(passenger))
            return false;

        if (!CanBoard(station, user, popup: true) || !CheckPlatformSide(station, passenger, user))
            return true;

        var self = passenger == user;
        var delay = self ? station.Comp.BoardDelay : station.Comp.DraggedBoardDelay;

        if (!self)
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-being-boarded", ("user", Identity.Entity(user, EntityManager))), passenger, passenger, PopupType.Large);
        }

        var doAfter = new DoAfterArgs(EntityManager, user, delay, new TransitPodBoardDoAfterEvent(), station, target: passenger, used: station)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    private bool CanBoard(Entity<TransitTubeStationComponent> station, EntityUid user, bool popup)
    {
        string? reason = null;

        if (station.Comp.DockedPod is not { } podUid || !TryComp<TransitPodComponent>(podUid, out var pod))
            reason = "transit-pod-none";
        else if (station.Comp.Depot)
            reason = "transit-station-depot";
        else if (!_access.IsAllowed(user, station))
            reason = "transit-station-access-denied";
        else if (pod.State != TransitPodState.Open)
            reason = "transit-pod-not-ready";
        else if (GetPassengers((podUid, pod)).Count() >= pod.MaxPassengers)
            reason = "transit-pod-full";

        if (reason != null && popup)
            _popup.PopupEntity(Loc.GetString(reason), station, user);

        return reason == null;
    }

    private bool OnPlatformSide(Entity<TransitTubeStationComponent> station, EntityUid uid)
    {
        // Held things sit at their carrier's position, so this covers them too.
        var stationXform = Transform(station);
        var offset = _xform.GetWorldPosition(uid) - _xform.GetWorldPosition(stationXform);
        var side = _xform.GetWorldRotation(stationXform).RotateVec(station.Comp.BoardingSide.ToVec());
        return System.Numerics.Vector2.Dot(offset, side) > 0.1f;
    }

    private bool CheckPlatformSide(Entity<TransitTubeStationComponent> station, EntityUid passenger, EntityUid user)
    {
        if (OnPlatformSide(station, passenger))
            return true;

        _popup.PopupEntity(Loc.GetString("transit-station-wrong-side"), station, user);
        return false;
    }

    [SubscribeLocalEvent]
    private void OnBoardDoAfter(Entity<TransitTubeStationComponent> ent, ref TransitPodBoardDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } passenger)
            return;

        if (!CanBoard(ent, args.User, popup: true) || !CheckPlatformSide(ent, passenger, args.User))
            return;

        if (ent.Comp.DockedPod is not { } podUid || !TryComp<TransitPodComponent>(podUid, out var pod))
            return;

        args.Handled = TryInsertPassenger((podUid, pod), passenger);

        // Let the new passenger pick where to go, if there is anywhere to pick.
        // It stays open for the ride, and can be opened again from the pod's menu.
        if (args.Handled && HasComp<MobStateComponent>(passenger) && passenger == args.User)
        {
            UpdateDestinations(ent);
            if (UpdatePodDestinations((podUid, pod)) > 0)
                _ui.OpenUi(podUid, TransitPodUiKey.Key, passenger);
        }
    }

    /// <summary>
    /// Puts an entity into a pod, giving it the pod's air.
    /// </summary>
    public bool TryInsertPassenger(Entity<TransitPodComponent?> pod, EntityUid passenger)
    {
        if (!Resolve(pod, ref pod.Comp) || !TryComp<DisposalHolderComponent>(pod, out var holder) || holder.Container == null)
            return false;

        Entity<TransitPodComponent> podEnt = (pod, pod.Comp);
        if (!CanCarry(pod.Comp, passenger)
            || GetPassengers(podEnt).Count() >= pod.Comp.MaxPassengers || !_container.Insert(passenger, holder.Container))
            return false;

        _holder.AttachEntity((pod, holder), passenger);
        UpdatePodVisuals(podEnt);
        return true;
    }

    [SubscribeLocalEvent]
    private void OnPodRelayMovement(Entity<TransitPodComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        // Each passenger has their own cooldown, used only by those who can actually move.
        if (!_actionBlocker.CanMove(args.Entity))
            return;

        var now = _timing.CurTime;
        if (ent.Comp.LastExitAttempts.TryGetValue(args.Entity, out var last) && now < last + ent.Comp.ExitAttemptCooldown)
            return;

        ent.Comp.LastExitAttempts[args.Entity] = now;

        switch (ent.Comp.State)
        {
            case TransitPodState.Open when ent.Comp.Station is { } stationUid && TryComp<TransitTubeStationComponent>(stationUid, out var station):
                Eject(ent, args.Entity, GetPlatform((stationUid, station)));
                break;

            // Stuck in the tube, or held at a signal: pry the pod open and climb out.
            case TransitPodState.Stalled:
            case TransitPodState.Moving when TryComp<DisposalHolderComponent>(ent, out var held) && held.Halted:
                var doAfter = new DoAfterArgs(EntityManager, args.Entity, ent.Comp.ClimbOutDelay, new TransitPodClimbOutDoAfterEvent(), ent, target: ent)
                {
                    BreakOnDamage = true,
                    NeedHand = false,
                };

                if (_doAfter.TryStartDoAfter(doAfter))
                    _popup.PopupEntity(Loc.GetString("transit-pod-climbing-out"), args.Entity, args.Entity);
                break;

            case TransitPodState.Moving:
                RequestStopFromInside(ent, args.Entity);
                break;
        }
    }

    [SubscribeLocalEvent]
    private void OnClimbOut(Entity<TransitPodComponent> ent, ref TransitPodClimbOutDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (ent.Comp.State != TransitPodState.Stalled
            && !(ent.Comp.State == TransitPodState.Moving && TryComp<DisposalHolderComponent>(ent, out var holder) && holder.Halted))
            return;

        Eject(ent, args.User, Transform(ent).Coordinates);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnPodActivate(Entity<TransitPodComponent> ent, ref ActivateInWorldEvent args)
    {
        // A pod stuck in the tube can be shoved back into motion from outside.
        if (args.Handled || !args.Complex || ent.Comp.State != TransitPodState.Stalled || IsPassenger(ent, args.User))
            return;

        args.Handled = true;

        if (TryPush(ent, args.User))
            _popup.PopupEntity(Loc.GetString("transit-pod-pushed"), ent, args.User);
        else
            _popup.PopupEntity(Loc.GetString("transit-pod-push-failed"), ent, args.User);
    }

    private bool TryPush(Entity<TransitPodComponent> pod, EntityUid user)
    {
        var xform = Transform(pod);
        if (!TryComp<DisposalHolderComponent>(pod, out var holder)
            || !TryGetTube(xform, out var tubeUid, holder)
            || !TryComp<DisposalTubeComponent>(tubeUid, out var tube)
            || xform.GridUid is not { } gridUid)
            return false;

        // Tube exits are directions on the grid, so the push is turned into the grid's frame.
        var push = _xform.GetWorldPosition(xform) - _xform.GetWorldPosition(user);
        push = (-_xform.GetWorldRotation(gridUid)).RotateVec(push);
        if (push.LengthSquared() < 0.01f)
            return false;

        push = System.Numerics.Vector2.Normalize(push);
        var exits = _disposalTube.GetTubeConnectableDirections((tubeUid, tube))
            .Where(exit => System.Numerics.Vector2.Dot(System.Numerics.Vector2.Normalize(exit.ToVec()), push) > 0.3f)
            .OrderByDescending(exit => System.Numerics.Vector2.Dot(System.Numerics.Vector2.Normalize(exit.ToVec()), push));

        foreach (var exit in exits)
        {
            if (!_holder.Launch((pod, holder), (tubeUid, tube), exit))
                continue;

            _xform.SetCoordinates(pod, Transform(tubeUid).Coordinates);
            pod.Comp.Bounced = false;
            SetState(pod, TransitPodState.Moving);
            _audio.PlayPvs(pod.Comp.DepartSound, pod);
            return true;
        }

        return false;
    }

    private EntityCoordinates GetPlatform(Entity<TransitTubeStationComponent> station)
    {
        var xform = Transform(station);
        var side = new Angle(station.Comp.BoardingSide.ToAngle() + xform.LocalRotation).GetDir();
        return xform.Coordinates.Offset(side.ToVec());
    }

    private IEnumerable<EntityUid> GetPassengers(Entity<TransitPodComponent> pod)
    {
        if (!TryComp<DisposalHolderComponent>(pod, out var holder) || holder.Container == null)
            return [];

        return holder.Container.ContainedEntities;
    }

    private void Eject(Entity<TransitPodComponent> pod, EntityUid passenger, EntityCoordinates destination)
    {
        if (!TryComp<DisposalHolderComponent>(pod, out var holder) || holder.Container == null)
            return;

        pod.Comp.LastExitAttempts.Remove(passenger);
        _ui.CloseUi(pod.Owner, TransitPodUiKey.Key, passenger);
        _holder.DetachEntity(passenger);
        _container.Remove(passenger, holder.Container, force: true, destination: destination);
        UpdatePodVisuals(pod);
    }

    private void EjectAll(Entity<TransitPodComponent> pod, EntityCoordinates destination)
    {
        // The whole grid or map is going away, passengers go with it.
        if (TerminatingOrDeleted(destination.EntityId) || TerminatingOrDeleted(Transform(pod).MapUid))
            return;

        foreach (var passenger in GetPassengers(pod).ToArray())
            Eject(pod, passenger, destination);
    }
}
