using System.Linq;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._Starlight.Mech;

public sealed partial class PilotSupportModuleSystem : EntitySystem
{
    [Dependency] private VehicleSystem _vehicle = default!;
    [Dependency] private ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PilotSupportModuleComponent, MechEquipmentInsertedEvent>(OnModuleInserted);
        SubscribeLocalEvent<PilotSupportModuleComponent, MechEquipmentRemovedEvent>(OnModuleRemoved);
    }

    private void OnModuleInserted(Entity<PilotSupportModuleComponent> ent, ref MechEquipmentInsertedEvent args)
        => RebuildWhitelist(args.Mech);

    private void OnModuleRemoved(Entity<PilotSupportModuleComponent> ent, ref MechEquipmentRemovedEvent args)
    {
        _vehicle.TryExit(args.Mech);
        RebuildWhitelist(args.Mech, ent);
    }

    private void RebuildWhitelist(EntityUid uid, EntityUid? removed = null)
    {
        if (!TryComp<MechComponent>(uid, out var mech) || !TryComp<VehicleComponent>(uid, out var vehicle))
            return;

        if (!mech.PilotSupportWhitelistInitialized)
        {
            mech.PilotSupportBaseWhitelist = vehicle.OperatorWhitelist;
            mech.PilotSupportWhitelistInitialized = true;
        }

        if (mech.PilotSupportBaseWhitelist == null)
            return;

        var whitelist = _serialization.CreateCopy(mech.PilotSupportBaseWhitelist, notNullableOverride: true);
        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (equipment == removed || !TryComp<PilotSupportModuleComponent>(equipment, out var module) ||
                module.PilotWhitelist is not { } addition)
                continue;
            if (addition.Components != null)
                whitelist.Components = (whitelist.Components ?? []).Concat(addition.Components).Distinct().ToArray();
            if (addition.Tags != null)
                whitelist.Tags = (whitelist.Tags ?? []).Concat(addition.Tags).Distinct().ToList();
        }

        _vehicle.SetOperatorWhitelist((uid, vehicle), whitelist);
    }
}
