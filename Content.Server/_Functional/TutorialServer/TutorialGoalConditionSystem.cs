using Content.Shared._Functional.TutorialServer;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.Utility;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Progress for live curriculum goals in the Character objectives window.
/// </summary>
public sealed partial class TutorialGoalConditionSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialGoalConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<TutorialGoalConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
    }

    private void OnAfterAssign(Entity<TutorialGoalConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        // Title/description are filled by TutorialServerRuleSystem after GoalIndex is set.
    }

    private void OnGetProgress(Entity<TutorialGoalConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(ent.Comp.GoalIndex, args.Mind);
    }

    /// <summary>
    /// Updates the objective title/description from the live participant tip.
    /// </summary>
    public void SyncObjectiveText(
        EntityUid objectiveUid,
        TutorialGoalConditionComponent cond,
        TutorialRolePrototype role,
        TutorialParticipantComponent part)
    {
        if (cond.GoalIndex < 0 || cond.GoalIndex >= role.Goals.Count)
            return;

        var goal = role.Goals[cond.GoalIndex];
        _meta.SetEntityName(objectiveUid, Loc.GetString(goal.Title));

        string description;
        if (part.GoalIndex > cond.GoalIndex)
            description = Loc.GetString("tutorial-server-objective-goal-done");
        else if (part.GoalIndex < cond.GoalIndex)
            description = Loc.GetString("tutorial-server-objective-goal-pending");
        else
            description = FormattedMessage.RemoveMarkupPermissive(part.StepText);

        _meta.SetEntityDescription(objectiveUid, description);
    }

    private float GetProgress(int goalIndex, MindComponent mind)
    {
        if (mind.OwnedEntity is not { } mob ||
            !TryComp<TutorialParticipantComponent>(mob, out var part))
            return 0f;

        if (part.GoalIndex > goalIndex)
            return 1f;

        if (part.GoalIndex < goalIndex)
            return 0f;

        var total = Math.Max(part.SubGoalCount, 1);
        return Math.Clamp(part.SubGoalIndex / (float) total, 0f, 0.99f);
    }
}
