using Content.Shared.Mech;
using Content.Shared.Actions;
using Content.Shared._Starlight.Mech.Equipment.Components;

namespace Content.Shared._Starlight.Mech.Equipment.EntitySystems;

public abstract partial class SharedMechEquipmentActionSystem : EntitySystem
{

    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MechEquipmentActionComponent, BeforePilotInsertEvent>(OnPilotInserted);
        SubscribeLocalEvent<MechEquipmentActionComponent, BeforePilotEjectEvent>(OnPilotEjecting);
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
    /// Actually handles adding the actions
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="comp"></param>
    /// <param name="pilot"></param>
    protected void GrantActions(EntityUid ent, MechEquipmentActionComponent comp, EntityUid pilot)
    {
        _actions.AddAction(pilot, ref comp.EquipmentActionEntity, comp.EquipmentAction, ent);
    }

    /// <summary>
    /// Actually handles removing actions
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="comp"></param>
    /// <param name="pilot"></param>
    protected void RemoveActions(EntityUid ent, MechEquipmentActionComponent comp, EntityUid pilot)
    {
        _actions.RemoveProvidedActions(pilot, ent);
    }
}
