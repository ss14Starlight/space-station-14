using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Actions;
using Content.Shared._Starlight.Mech.Equipment.EntitySystems;
using Content.Shared.Mech.Equipment.Components;

namespace Content.Server._Starlight.Mech.Equipment.EntitySystems;

/// <summary>
/// System for handling of actions provided by MechEquipmentActionComponent
/// </summary>
public sealed class MechEquipmentActionSystem : SharedMechEquipmentActionSystem
{
    [Dependency] private readonly SharedMechSystem _mech = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechEquipmentActionComponent, BeforePilotInsertEvent>(OnPilotInserted);
        SubscribeLocalEvent<MechEquipmentActionComponent, BeforePilotEjectEvent>(OnPilotEjecting);
        SubscribeLocalEvent<MechEquipmentActionComponent, MechEquipmentInsertedEvent>(OnEquipmentInserted);
        SubscribeLocalEvent<MechEquipmentActionComponent, MechEquipmentRemovedEvent>(OnEquipmentRemoved);
    }

    /// <summary>
    /// Adds actions to the pilot when a pilot first enters the mech
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="comp"></param>
    /// <param name="args"></param>
    private void OnPilotInserted(EntityUid ent, MechEquipmentActionComponent comp, ref BeforePilotInsertEvent args)
    {
        GrantActions(ent, comp, args.Pilot);
    }

    /// <summary>
    /// Removes actions from the pilot when they are about to exit the mech
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="comp"></param>
    /// <param name="args"></param>
    private void OnPilotEjecting(EntityUid ent, MechEquipmentActionComponent comp, ref BeforePilotEjectEvent args)
    {
        RemoveActions(ent, comp, args.Pilot);
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
            || _mech.IsEmpty(mechComp))
            return;

        GrantActions(ent, comp, mechComp.PilotSlot.ContainedEntity!.Value);
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
            || _mech.IsEmpty(mechComp))
            return;

        RemoveActions(ent, comp, mechComp.PilotSlot.ContainedEntity!.Value);
    }

    /// <summary>
    /// Actually handles adding the actions
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="comp"></param>
    /// <param name="pilot"></param>
    private void GrantActions(EntityUid ent, MechEquipmentActionComponent comp, EntityUid pilot)
    {
        _actions.AddAction(pilot, ref comp.EquipmentActionEntity, comp.EquipmentAction, ent);
    }

    /// <summary>
    /// Actually handles removing actions
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="comp"></param>
    /// <param name="pilot"></param>
    private void RemoveActions(EntityUid ent, MechEquipmentActionComponent comp, EntityUid pilot)
    {
        _actions.RemoveProvidedActions(pilot, ent);
    }

}
