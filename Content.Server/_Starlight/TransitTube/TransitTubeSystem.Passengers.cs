using System.Linq;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Unit;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Robust.Shared.Map;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    [SubscribeLocalEvent]
    private void OnStationActivate(Entity<TransitTubeStationComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = TryRequestStop(ent, args.User) || TryBoard(ent, args.User, args.User);
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
        if (!HasComp<MobStateComponent>(passenger))
            return false;

        if (!CanBoard(station, user, popup: true))
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
        else if (pod.State != TransitPodState.Open)
            reason = "transit-pod-not-ready";
        else if (GetPassengers((podUid, pod)).Count() >= pod.MaxPassengers)
            reason = "transit-pod-full";

        if (reason != null && popup)
            _popup.PopupEntity(Loc.GetString(reason), station, user);

        return reason == null;
    }

    [SubscribeLocalEvent]
    private void OnBoardDoAfter(Entity<TransitTubeStationComponent> ent, ref TransitPodBoardDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } passenger)
            return;

        if (!CanBoard(ent, args.User, popup: true))
            return;

        if (ent.Comp.DockedPod is not { } podUid || !TryComp<TransitPodComponent>(podUid, out var pod))
            return;

        args.Handled = TryInsertPassenger((podUid, pod), passenger);
    }

    /// <summary>
    /// Puts an entity into a pod, giving it the pod's air.
    /// </summary>
    public bool TryInsertPassenger(Entity<TransitPodComponent> pod, EntityUid passenger)
    {
        if (!TryComp<DisposalHolderComponent>(pod, out var holder)
            || holder.Container == null
            || GetPassengers(pod).Count() >= pod.Comp.MaxPassengers
            || !_container.Insert(passenger, holder.Container))
            return false;

        _holder.AttachEntity((pod, holder), passenger);
        UpdatePodVisuals(pod);
        return true;
    }

    [SubscribeLocalEvent]
    private void OnPodRelayMovement(Entity<TransitPodComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.LastExitAttempt + ent.Comp.ExitAttemptCooldown)
            return;

        ent.Comp.LastExitAttempt = now;

        if (!_actionBlocker.CanMove(args.Entity))
            return;

        switch (ent.Comp.State)
        {
            case TransitPodState.Open when ent.Comp.Station is { } stationUid
                                           && TryComp<TransitTubeStationComponent>(stationUid, out var station):
                Eject(ent, args.Entity, GetPlatform((stationUid, station)));
                break;

            case TransitPodState.Stalled:
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
        if (args.Handled || args.Cancelled || ent.Comp.State != TransitPodState.Stalled)
            return;

        Eject(ent, args.User, Transform(ent).Coordinates);
        args.Handled = true;
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
