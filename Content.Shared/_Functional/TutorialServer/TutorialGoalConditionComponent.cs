namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Live curriculum goal shown in the Character objectives window.
/// Progress tracks the player's <see cref="TutorialParticipantComponent"/> for <see cref="GoalIndex"/>.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialGoalConditionComponent : Component
{
    /// <summary>
    /// Index into the role's <c>Goals</c> list this objective represents.
    /// </summary>
    [DataField]
    public int GoalIndex;
}
