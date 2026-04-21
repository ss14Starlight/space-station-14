using Content.Shared.Actions;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared._Starlight.Mech.Equipment.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech;
using Content.Shared.Eye.Blinding.Components;

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

        SubscribeLocalEvent<MechNightVisionComponent, MechToggleNightVisionEvent>(OnNightVisionToggle);

        SubscribeLocalEvent<MechNightVisionComponent, BeforePilotEjectEvent>(OnPilotEject);
    }

    private void OnNightVisionToggle(EntityUid uid, MechNightVisionComponent comp, ref MechToggleNightVisionEvent args)
    {
        if (!TryComp<MechEquipmentComponent>(uid, out var equipmentComp)
            || equipmentComp.EquipmentOwner == null
            || !TryComp<MechComponent>(equipmentComp.EquipmentOwner, out var mechComp)
            || mechComp.PilotSlot.ContainedEntity == null)
            return;

        if (!comp.EquipmentToggled && !HasComp<NightVisionComponent>(mechComp.PilotSlot.ContainedEntity.Value))
        {
            AddComp<NightVisionComponent>(mechComp.PilotSlot.ContainedEntity.Value);
            comp.EquipmentComponentAdded = true;
        }
        else
        {
            if (comp.EquipmentComponentAdded) // Only remove if we actually added this component
                RemComp<NightVisionComponent>(mechComp.PilotSlot.ContainedEntity.Value);
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

        if (TryComp<MechEquipmentActionComponent>(uid, out var actionComp)) // TODO: move elsewhere
        {
            _actions.SetToggled(actionComp.EquipmentActionEntity, false);
        }
    }
}
