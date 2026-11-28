using Content.Shared._Starlight.Vehicle;
using Content.Shared.Actions;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.UserInterface;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Systems;

namespace Content.Server._Starlight.Vehicle;

public sealed partial class VehicleRadarSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private VehicleSystem _vehicles = default!;

    [SubscribeLocalEvent]
    private void OnOperatorChanged(Entity<VehicleRadarComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator != null)
            _actions.RemoveAction(ent.Comp.ActionEntity);

        if (args.NewOperator is { } driver)
            _actions.AddAction(driver, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnRadarToggle(EntityUid uid, VehicleRadarComponent component, ToggleVehicleRadarEvent args)
    {
        if (!args.Handled && _vehicles.GetOperatorOrNull(uid) == args.Performer)
            args.Handled = _ui.TryToggleUi(uid, RadarConsoleUiKey.Key, args.Performer);
    }
}
