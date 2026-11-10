using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.Objectives.Components;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared.Destructible;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Objectives;
using Content.Server._Starlight.Railroading;
using Content.Shared._Starlight.Objectives.Events;
using Content.Shared._Starlight.Railroading.Components;

namespace Content.Server._Starlight.Objectives.Systems;

/// <summary>
/// Handles persistent defeat progress for Teach a Lesson objectives, including station AI shutdown.
/// </summary>
public sealed partial class TeachALessonConditionSystem : EntitySystem
{
    [Dependency] private RailroadingSystem _railroad = default!;
    [Dependency] private SharedStationAiSystem _stationAi = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TeachALessonTargetComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<TeachALessonConditionComponent, ObjectiveAfterAssignEvent>((ent, ref _) => OnAfterAssign(ent));
        SubscribeLocalEvent<TeachALessonConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);

        SubscribeLocalEvent<TeachALessonConditionComponent, RailroadingCardChosenEvent>((ent, ref _) => OnAfterAssign(ent));
        SubscribeLocalEvent<TeachALessonConditionComponent, RailroadingCardCompletionQueryEvent>(OnTaskCompletionQuery);
        SubscribeLocalEvent<TeachALessonConditionComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);
    }

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

    private void OnGetProgress(Entity<TeachALessonConditionComponent> ent, ref ObjectiveGetProgressEvent args)
        => args.Progress = ent.Comp.HasDied ? 1.0f : 0.0f;

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

        var targetEntity = targetMobUid.Value;
        if (_stationAi.TryGetCore(targetEntity, out var aiCore))
        {
            targetEntity = targetMindUid.Value;
            if (TryComp<DestructibleComponent>(aiCore.Owner, out var destructible) && destructible.IsBroken)
                MarkTeacherComplete(ent.Owner);
        }

        var targetComponent = EnsureComp<TeachALessonTargetComponent>(targetEntity);
        targetComponent.Teachers.Add(ent);
    }

    private void OnMobStateChanged(Entity<TeachALessonTargetComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        foreach (var teacher in ent.Comp.Teachers)
            MarkTeacherComplete(teacher);
    }
    [SubscribeLocalEvent]
    private void OnDamageThresholdReached(
        EntityUid uid,
        StationAiCoreComponent component,
        DamageThresholdReached args)
    {
        foreach (var behavior in args.Threshold.Behaviors)
        {
            if (behavior is not DoActsBehavior doActsBehavior ||
                !doActsBehavior.HasAct(ThresholdActs.Breakage))
                continue;

            if (!_stationAi.TryGetHeld((uid, component), out var held) ||
                !_mind.TryGetMind(held.Value, out var mindId, out _) ||
                !TryComp(mindId, out TeachALessonTargetComponent? target))
                return;

            foreach (var teacher in target.Teachers)
                MarkTeacherComplete(teacher);

            return;
        }
    }

    private void MarkTeacherComplete(EntityUid teacher)
    {
        if (!TryComp(teacher, out TeachALessonConditionComponent? condition) || condition.HasDied)
            return;

        condition.HasDied = true;

        if (TryComp<RailroadableComponent>(teacher, out var railroadable) &&
            railroadable.ActiveCard is not null)
            _railroad.InvalidateProgress((teacher, railroadable));
    }
}
