using Content.Shared._Functional.TutorialServer;
using Content.Shared.Objectives.Components;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Keeps tutorial placeholder objectives at 0 progress so they illustrate objectives in Character UI.
/// </summary>
public sealed class TutorialPlaceholderConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialPlaceholderConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<TutorialPlaceholderConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 0f;
    }
}
