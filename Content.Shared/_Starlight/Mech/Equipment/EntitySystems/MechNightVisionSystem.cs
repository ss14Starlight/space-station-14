using Content.Shared.Actions;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared._Starlight.Mech.Equipment.Components;

namespace Content.Shared._Starlight.Mech.Equipment.EntitySystems;

/// <summary>
///
/// </summary>
public sealed class MechNightVisionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

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
            || !TryComp<MechComponent>(equipmentComp.EquipmentOwner, out var mechComp)
            || mechComp.PilotSlot.ContainedEntity == null)
            return;

        if (HasComp<NightVisionComponent>(mechComp.PilotSlot.ContainedEntity.Value) && comp.EquipmentComponentAdded)
            RemComp<NightVisionComponent>(mechComp.PilotSlot.ContainedEntity.Value);
        comp.EquipmentComponentAdded = false;
        comp.EquipmentToggled = false;
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
            || !TryComp<MechComponent>(equipmentComp.EquipmentOwner, out var mechComp)
            || mechComp.PilotSlot.ContainedEntity == null)
            return;

        var pilot = mechComp.PilotSlot.ContainedEntity.Value;
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
    }

    private void OnPilotEject(EntityUid uid, MechNightVisionComponent component, ref BeforePilotEjectEvent args)
    {

        if (HasComp<NightVisionComponent>(args.Pilot) && component.EquipmentToggled && component.EquipmentComponentAdded)
            RemComp<NightVisionComponent>(args.Pilot);

        component.EquipmentComponentAdded = false;
        component.EquipmentToggled = false;

        if (TryComp<MechEquipmentActionComponent>(uid, out var actionComp))
            _actions.SetToggled(actionComp.EquipmentActionEntity, false);
    }
}
