using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using System.Linq;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Whitelist;

namespace Content.Shared._Starlight.Mech;

public sealed class PilotSupportModuleSystem : EntitySystem
{
    [Dependency] private readonly SharedMechSystem _mech = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PilotSupportModuleComponent, MechEquipmentRemovedEvent>(OnModuleRemoved);
        SubscribeLocalEvent<PilotSupportModuleComponent, MechEquipmentInsertedEvent>(OnModuleInserted);
    }

    private void OnModuleInserted(Entity<PilotSupportModuleComponent> entity, ref MechEquipmentInsertedEvent args)
    {
        if (TryComp<MechComponent>(args.Mech, out var mech) && TryComp<PilotSupportModuleComponent>(entity, out var module))
        {
            if (mech.PilotWhitelist != null && module.PilotWhitelist != null)
            {
                if (mech.PilotWhitelist.Components != null && module.PilotWhitelist.Components != null)
                {
                    mech.PilotWhitelist.Components = mech.PilotWhitelist.Components.Concat(module.PilotWhitelist.Components).ToArray();
                    _whitelist.ClearRegistrations(mech.PilotWhitelist);
                }

                if (mech.PilotWhitelist.Tags != null && module.PilotWhitelist.Tags != null)
                {
                    mech.PilotWhitelist.Tags.AddRange(module.PilotWhitelist.Tags);
                }
            }
            
            mech.Dirty();
        }
    }
    
    private void OnModuleRemoved(Entity<PilotSupportModuleComponent> entity, ref MechEquipmentRemovedEvent args)
    {
        if (TryComp<MechComponent>(args.Mech, out var mech) && TryComp<PilotSupportModuleComponent>(entity, out var module))
        {
            _mech.TryEject(args.Mech, mech);
            
            if (mech.PilotWhitelist != null && module.PilotWhitelist != null)
            {
                if (mech.PilotWhitelist.Components != null && module.PilotWhitelist.Components != null)
                {
                    mech.PilotWhitelist.Components = mech.PilotWhitelist.Components.Except(module.PilotWhitelist.Components).ToArray();
                    _whitelist.ClearRegistrations(mech.PilotWhitelist);
                }

                if (mech.PilotWhitelist.Tags != null && module.PilotWhitelist.Tags != null)
                {
                    mech.PilotWhitelist.Tags.RemoveAll(whitelist => module.PilotWhitelist.Tags.Contains(whitelist));
                }
            }

            mech.Dirty();
        }
    }
}