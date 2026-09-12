using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Handheld tutorial prompt device for travel/off-grid roles. Activating it opens the Bound UI;
/// it also speaks the current tip on a reminder cadence.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class TutorialGuideComponent : Component
{
    /// <summary>
    /// Goal currently displayed in the Bound UI (kept snapped to live progress).
    /// </summary>
    [ViewVariables]
    public int ViewGoalIndex;

    /// <summary>
    /// Sub-goal / legacy step currently displayed in the Bound UI.
    /// </summary>
    [ViewVariables]
    public int ViewIndex;

    /// <summary>
    /// Sub-goal id of the last line spoken (change detection; no timed reminders).
    /// </summary>
    [DataField]
    public string? LastSpokenSubGoal;

    /// <summary>
    /// Unused legacy field kept for map/component compatibility.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextReminderAt;
}
