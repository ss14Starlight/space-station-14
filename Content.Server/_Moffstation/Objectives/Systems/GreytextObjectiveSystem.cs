using Content.Server._Moffstation.Objectives.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server._Moffstation.Objectives.Systems;
/// <summary>
/// This system/component is meant for setting an objective to always greytext. Useful for cases where the title for an open-ended objective requires some logic
/// </summary>
public sealed class GreytextObjectiveSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GreytextObjectiveComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<GreytextObjectiveComponent> ent , ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress();
    }

    private float GetProgress()
    {
        return 0f;
    }

}
