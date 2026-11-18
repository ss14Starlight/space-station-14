using Content.Shared.Vehicle.Systems;
using Content.Shared.Actions;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech;
using Content.Shared._Starlight.Mech.Equipment.Components;
using Content.Shared._Starlight.Overlay.Components;

namespace Content.Shared._Starlight.Mech.Equipment.EntitySystems;

/// <summary>
///
/// </summary>
public sealed partial class MechNightVisionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechNightVisionComponent, MechEquipmentRemovedEvent>(OnEquipmentRemoved);
        SubscribeLocalEvent<MechNightVisionComponent, MechToggleNightVisionEvent>(OnNightVisionToggle);

        SubscribeLocalEvent<MechNightVisionComponent, BeforePilotEjectEvent>(OnPilotEject);
    }

    /// <summary>
    /// Removes added component(s) from the pilot when equipment is removed from the mech
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="comp"></param>
    /// <param name="args"></param>
    private void OnEquipmentRemoved(EntityUid uid, MechNightVisionComponent comp, ref MechEquipmentRemovedEvent args)
    {
        if (!TryComp<MechEquipmentComponent>(uid, out var equipmentComp)
            || equipmentComp.EquipmentOwner == null
            || _vehicle.GetOperatorOrNull(equipmentComp.EquipmentOwner.Value) is not { } pilot)
            return;

        if (HasComp<NightVisionComponent>(pilot) && comp.EquipmentComponentAdded)
            RemComp<NightVisionComponent>(pilot);
        comp.EquipmentComponentAdded = false;
        comp.EquipmentToggled = false;
        Dirty(uid, comp);
    }

    /// <summary>
    /// Handles adding/removing night vision component to the pilot
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="comp"></param>
    /// <param name="args"></param>
    private void OnNightVisionToggle(EntityUid uid, MechNightVisionComponent comp, MechToggleNightVisionEvent args)
    {
        if (!TryComp<MechEquipmentComponent>(uid, out var equipmentComp)
            || equipmentComp.EquipmentOwner == null
            || _vehicle.GetOperatorOrNull(equipmentComp.EquipmentOwner.Value) is not { } pilot)
            return;

        if (!comp.EquipmentToggled && !HasComp<NightVisionComponent>(pilot))
        {
            AddComp<NightVisionComponent>(pilot);
            comp.EquipmentComponentAdded = true;
        }
        else if (comp.EquipmentToggled)
        {
            if (HasComp<NightVisionComponent>(pilot) && comp.EquipmentComponentAdded) // Only remove if we actually added this component
                RemComp<NightVisionComponent>(pilot);
            comp.EquipmentComponentAdded = false;
        }

        comp.EquipmentToggled = !comp.EquipmentToggled;

        _actions.SetToggled((args.Action.Owner, args.Action.Comp), comp.EquipmentToggled);
        Dirty(uid, comp);
    }

    private void OnPilotEject(EntityUid uid, MechNightVisionComponent component, ref BeforePilotEjectEvent args)
    {

        if (HasComp<NightVisionComponent>(args.Pilot) && component.EquipmentToggled && component.EquipmentComponentAdded)
            RemComp<NightVisionComponent>(args.Pilot);

        component.EquipmentComponentAdded = false;
        component.EquipmentToggled = false;
        Dirty(uid, component);

        if (TryComp<MechEquipmentActionComponent>(uid, out var actionComp))
            _actions.SetToggled(actionComp.EquipmentActionEntity, false);
    }
}
