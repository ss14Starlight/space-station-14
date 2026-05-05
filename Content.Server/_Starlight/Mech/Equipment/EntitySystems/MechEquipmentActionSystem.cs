using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared._Starlight.Mech.Equipment.Components;
using Content.Shared._Starlight.Mech.Equipment.EntitySystems;

namespace Content.Server._Starlight.Mech.Equipment.EntitySystems;

/// <summary>
/// System for handling of actions provided by MechEquipmentActionComponent
/// </summary>
public sealed class MechEquipmentActionSystem : SharedMechEquipmentActionSystem
{

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechEquipmentActionComponent, MechEquipmentInsertedEvent>(OnEquipmentInserted);
        SubscribeLocalEvent<MechEquipmentActionComponent, MechEquipmentRemovedEvent>(OnEquipmentRemoved);
    }

    /// <summary>
    /// Adds actions to the pilot, if any, when the equipment is inserted into the mech
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="comp"></param>
    /// <param name="args"></param>
    private void OnEquipmentInserted(EntityUid ent, MechEquipmentActionComponent comp, ref MechEquipmentInsertedEvent args)
    {
        if (!TryComp<MechComponent>(args.Mech, out var mechComp)
            || !mechComp.PilotSlot.ContainedEntity.HasValue)
            return;

        GrantActions(ent, comp, mechComp.PilotSlot.ContainedEntity.Value);
    }

    /// <summary>
    /// Removes actions from the pilot, if any, when equipment is removed from the mech
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="comp"></param>
    /// <param name="args"></param>
    private void OnEquipmentRemoved(EntityUid ent, MechEquipmentActionComponent comp, ref MechEquipmentRemovedEvent args)
    {
        if (!TryComp<MechComponent>(args.Mech, out var mechComp)
            || !mechComp.PilotSlot.ContainedEntity.HasValue)
            return;

        RemoveActions(ent, comp, mechComp.PilotSlot.ContainedEntity.Value);
    }

}
