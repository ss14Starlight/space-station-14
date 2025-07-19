using Content.Server._Moffstation.Objectives.Components;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.Objectives.Systems;

public sealed class LocationObjectiveSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocationObjectiveComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
    }

    private void OnAfterAssign(Entity<LocationObjectiveComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (ent.Comp.Target is not {} target)
            return;

        var departmentName = Loc.GetString(target);
        _metaData.SetEntityName(ent.Owner, Loc.GetString(ent.Comp.Title, ("targetName", departmentName)), args.Meta);
    }
}
