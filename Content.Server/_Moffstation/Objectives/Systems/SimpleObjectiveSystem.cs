using Content.Server._Moffstation.Objectives.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server._Moffstation.Objectives.Systems;
/// <summary>
/// This is a system/component that will take strings and plop them straight into the objective system, without conditions and the like.
/// Good for freeform objectives that dont have varying targets.
/// </summary>
public sealed class SimpleObjectiveSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SimpleObjectiveComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<SimpleObjectiveComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
    }

    private void OnGetProgress(Entity<SimpleObjectiveComponent> ent , ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress();
    }

    private float GetProgress()
    {
        return 0f;
    }

    private void OnAfterAssign(EntityUid uid, SimpleObjectiveComponent comp, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(uid, GetTitle(comp.Title), args.Meta);
    }

    private string GetTitle(string title)
    {
        return Loc.GetString(title);
    }
}
