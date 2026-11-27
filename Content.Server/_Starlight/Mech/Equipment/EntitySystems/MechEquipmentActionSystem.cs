using Content.Shared.Vehicle.Systems;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared._Starlight.Mech.Equipment.Components;
using Content.Shared._Starlight.Mech.Equipment.EntitySystems;

namespace Content.Server._Starlight.Mech.Equipment.EntitySystems;

/// <summary>
/// System for handling of actions provided by MechEquipmentActionComponent
/// </summary>
public sealed partial class MechEquipmentActionSystem : SharedMechEquipmentActionSystem
{
    [Dependency] private VehicleSystem _vehicle = default!;

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
        if (_vehicle.GetOperatorOrNull(args.Mech) is not { } pilot)
            return;

        GrantActions(ent, comp, pilot);
    }

    /// <summary>
    /// Removes actions from the pilot, if any, when equipment is removed from the mech
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="comp"></param>
    /// <param name="args"></param>
    private void OnEquipmentRemoved(EntityUid ent, MechEquipmentActionComponent comp, ref MechEquipmentRemovedEvent args)
    {
        if (_vehicle.GetOperatorOrNull(args.Mech) is not { } pilot)
            return;

        RemoveActions(ent, comp, pilot);
    }
}
