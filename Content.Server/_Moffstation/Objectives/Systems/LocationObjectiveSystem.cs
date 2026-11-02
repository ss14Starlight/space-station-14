using Content.Server._Moffstation.Objectives.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server._Moffstation.Objectives.Systems;

public sealed partial class LocationObjectiveSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!; // Starlight
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocationObjectiveComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
    }

    private void OnAfterAssign(Entity<LocationObjectiveComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (ent.Comp.Target is not {} target)
            return;

        _metaData.SetEntityName(ent.Owner, Loc.GetString(ent.Comp.Title, ("targetName", target)), args.Meta); // Starlight
    }
}
