using Content.Server.Power.EntitySystems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Tags crop chem machines so InteractTargetTag goals hit the real bench instead of
/// spawning duplicate dispensers/masters/grinders on top of tables and walls.
/// Also powers the crop hotplate so heated recipes (table salt) actually cook.
/// </summary>
public sealed partial class TutorialChemBootstrapSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> ChemDispenserTag = "TutorialChemDispenser";
    private static readonly ProtoId<TagPrototype> ChemMasterTag = "TutorialChemMaster";
    private static readonly ProtoId<TagPrototype> GrinderTag = "TutorialGrinder";
    private static readonly ProtoId<TagPrototype> HotplateTag = "TutorialHotplate";

    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private TagSystem _tags = default!;

    public void TryConfigureOnGrid(EntityUid gridUid, TutorialRolePrototype role)
    {
        if (role.ID != "TutorialChemist")
            return;

        var query = EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var xform, out var meta))
        {
            if (xform.GridUid != gridUid)
                continue;

            switch (meta.EntityPrototype?.ID)
            {
                case "ChemDispenser":
                case "TutorialChemDispenser":
                    _tags.AddTag(uid, ChemDispenserTag);
                    _power.SetNeedsPower(uid, false);
                    break;
                case "ChemMaster":
                case "TutorialChemMaster":
                    _tags.AddTag(uid, ChemMasterTag);
                    _power.SetNeedsPower(uid, false);
                    break;
                case "KitchenReagentGrinder":
                case "TutorialKitchenReagentGrinder":
                    _tags.AddTag(uid, GrinderTag);
                    _power.SetNeedsPower(uid, false);
                    break;
                case "ChemistryHotplate":
                    _tags.AddTag(uid, HotplateTag);
                    _power.SetNeedsPower(uid, false);
                    break;
            }
        }
    }
}
