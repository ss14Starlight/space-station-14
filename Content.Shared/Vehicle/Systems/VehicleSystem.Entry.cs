using Content.Shared._Starlight.Vehicle;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;

namespace Content.Shared.Vehicle.Systems;

public sealed partial class VehicleSystem
{
    [Dependency] private SharedInteractionSystem _entryInteraction = default!; // Starlight
    [SubscribeLocalEvent]
    private void OnCanDragDrop(Entity<ContainerVehicleEntryComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.CanDrop |= CanEnterViaInteraction(ent.Owner, args.Dragged, args.User); // Starlight
    }

    [SubscribeLocalEvent]
    private void OnDragDrop(Entity<ContainerVehicleEntryComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        StartEntryInteraction(ent, args.User, args.Dragged); // Starlight
    }

    [SubscribeLocalEvent]
    private void OnAlternativeVerb(Entity<ContainerVehicleEntryComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract)
            return;

        var user = args.User;
        if (CanEnterViaInteraction(ent.Owner, user, user)) // Starlight
        {
            var enterVerb = new AlternativeVerb
            {
                Text = Loc.GetString("container-vehicle-verb-enter"),
                Act = () => StartEntryInteraction(ent, user, user) // Starlight
            };
            args.Verbs.Add(enterVerb);
        }
        else if (CanExitViaInteraction(ent.Owner, user))
        {
            var exitVerb = new AlternativeVerb
            {
                Text = Loc.GetString("container-vehicle-verb-remove-operator"),
                Priority = 1,
                Act = () => StartExitInteraction(ent, user)
            };
            args.Verbs.Add(exitVerb);
        }
    }

    [SubscribeLocalEvent]
    private void OnEntryCompleted(EntityUid uid, ContainerVehicleEntryComponent component, ContainerVehicleEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        #region Starlight
        var entering = args.Target is { } target && target != uid ? target : args.User;
        if (!_actionBlocker.CanInteract(args.User, uid) ||
            !_entryInteraction.InRangeUnobstructed(args.User, uid) ||
            (entering != args.User && !_entryInteraction.InRangeUnobstructed(args.User, entering)) ||
            !CanEnterViaInteraction(uid, entering, args.User) ||
            !TryValidateEntryOperator((uid, component), entering, args.User) ||
            !TryGetOperatorContainer(uid, out var container))
            return;

        args.Handled = _container.Insert(entering, container);
        #endregion
    }

    [SubscribeLocalEvent]
    private void OnExitCompleted(EntityUid uid, ContainerVehicleEntryComponent component, ContainerVehicleExitEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (GetOperatorOrNull(uid) is not { } driver || GetNetEntity(driver) != args.Operator || // Starlight
            !CanExitViaInteraction(uid, args.User) || !_entryInteraction.InRangeUnobstructed(args.User, uid) || // Starlight
            !TryExit(uid))
            return;

        args.Handled = true;
    }

    private void StartEntryInteraction(Entity<ContainerVehicleEntryComponent> vehicle, EntityUid user, EntityUid entering) // Starlight
    {
        if (!CanEnterViaInteraction(vehicle.Owner, entering, user) || // Starlight
            !TryValidateEntryOperator(vehicle, entering, user)) // Starlight
            return;

        var doAfterEventArgs = new DoAfterArgs(
            EntityManager,
            user, // Starlight
            vehicle.Comp.EntryDelay,
            new ContainerVehicleEntryEvent(),
            vehicle.Owner,
            target: user == entering ? vehicle.Owner : entering) // Starlight
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private bool TryValidateEntryOperator(Entity<ContainerVehicleEntryComponent> vehicle, EntityUid entering, EntityUid user) // Starlight
    {
        if (HasOperator(vehicle.Owner) || CanOperate(vehicle.Owner, entering)) // Starlight
            return true;

        _popup.PopupEntity(Loc.GetString(vehicle.Comp.EntryDeniedPopup, ("vehicle", vehicle.Owner)),
            Identity.Entity(user, EntityManager));
        return false;
    }

    private void StartExitInteraction(Entity<ContainerVehicleEntryComponent> vehicle, EntityUid user)
    {
        if (!CanExitViaInteraction(vehicle.Owner, user) ||
            !TryGetOperator(vehicle.Owner, out var operatorEnt))
            return;

        if (user == vehicle.Owner || user == operatorEnt.Value.Owner)
        {
            TryExit(vehicle.Owner);
            return;
        }

        var doAfterEventArgs = new DoAfterArgs(
            EntityManager,
            user,
            vehicle.Comp.ExitDelay,
            new ContainerVehicleExitEvent(GetNetEntity(operatorEnt.Value.Owner)), // Starlight
            vehicle.Owner,
            target: vehicle.Owner)
        {
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterEventArgs))
            return;

        _popup.PopupEntity(Loc.GetString(vehicle.Comp.OperatorRemovalPopup,
            ("vehicle", vehicle.Owner), ("user", Identity.Entity(user, EntityManager))),
            vehicle.Owner, PopupType.Large);
    }

    private bool CanEnterViaInteraction(EntityUid vehicle, EntityUid entering, EntityUid user) // Starlight
    {
        var allowImmobile = user != entering && HasComp<VehiclePassengerSeatsComponent>(vehicle); // Starlight
        if (!_vehicleQuery.TryComp(vehicle, out var vehicleComponent) ||
            !CanEnterContainer((vehicle, vehicleComponent), entering, allowImmobile)) // Starlight
            return false;

        var attempt = new ContainerVehicleEntryAttemptEvent(entering);
        RaiseLocalEvent(vehicle, ref attempt);
        return !attempt.Cancelled;
    }

    private bool CanExitViaInteraction(EntityUid vehicle, EntityUid user)
    {
        if (!HasOperator(vehicle))
            return false;

        var attempt = new ContainerVehicleExitAttemptEvent(user);
        RaiseLocalEvent(vehicle, ref attempt);
        return !attempt.Cancelled;
    }
}
