using Content.Server.Objectives.Components;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Objectives.Components;
using Content.Shared._Starlight.Railroading.Events;
using Content.Server._Starlight.Objectives.Events;
using Content.Shared._Starlight.Railroading;
using Content.Shared.Objectives;
using Content.Server._Starlight.Railroading;

namespace Content.Server._Starlight.Objectives.Systems;

/// <summary>
/// Handles Teach a Lesson logic on if a specific entity has died at least once during the round
/// </summary>
public sealed class TeachALessonConditionSystem : EntitySystem
{
    [Dependency] private readonly RailroadingSystem _railroad = default!; // Starlight
    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<TeachALessonTargetComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<TeachALessonConditionComponent, ObjectiveAfterAssignEvent>((ent, ref _) => OnAfterAssign(ent));
        SubscribeLocalEvent<TeachALessonConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);

        // Starlight - Start
        SubscribeLocalEvent<TeachALessonConditionComponent, RailroadingCardChosenEvent>((ent, ref _) => OnAfterAssign(ent));
        SubscribeLocalEvent<TeachALessonConditionComponent, RailroadingCardCompletionQueryEvent>(OnTaskCompletionQuery);
        SubscribeLocalEvent<TeachALessonConditionComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);
        // Starlight - End
    }

    // Starlight Start
    private void OnCollectObjectiveInfo(Entity<TeachALessonConditionComponent> ent, ref CollectObjectiveInfoEvent args)
    {
        if (!HasComp<RailroadCardComponent>(ent.Owner) || !TryComp<TargetObjectiveComponent>(ent.Owner, out var target))
            return;

        args.Objectives.Add(new ObjectiveInfo
        {
            Title = target.Title,
            Icon = target.Icon,
            Progress = ent.Comp.HasDied ? 1.0f : 0.0f,
        });
    }

    private void OnTaskCompletionQuery(Entity<TeachALessonConditionComponent> ent, ref RailroadingCardCompletionQueryEvent args)
    {
        if (args.IsCompleted == false) return;

        args.IsCompleted = ent.Comp.HasDied;
    }
    // Starlight End

    private void OnGetProgress(Entity<TeachALessonConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.HasDied ? 1.0f : 0.0f;
    }

    private void OnAfterAssign(Entity<TeachALessonConditionComponent> ent)
    {
        if (!TryComp(ent.Owner, out TargetObjectiveComponent? targetObjective))
            return;
        var targetMindUid = targetObjective.Target;
        if (targetMindUid is null)
            return;
        if (!TryComp(targetMindUid, out MindComponent? targetMind))
            return;
        var targetMobUid = targetMind.CurrentEntity;
        if (targetMobUid is null)
            return;
        var targetComponent = EnsureComp<TeachALessonTargetComponent>(targetMobUid.Value);
        targetComponent.Teachers.Add(ent);
        
    }

    private void OnMobStateChanged(Entity<TeachALessonTargetComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead) 
            return;
        foreach (var teacher in ent.Comp.Teachers)
        {
            if(!TryComp(teacher, out TeachALessonConditionComponent? condition))
                continue;

            condition.HasDied = true;

            // Starlight
            if (TryComp<RailroadableComponent>(teacher, out var railroadable)
            && railroadable.ActiveCard is not null)
                _railroad.InvalidateProgress((teacher, railroadable));
        }
    }
}