using Robust.Shared.Serialization;

namespace Content.Shared._Functional.TutorialServer;

[Serializable, NetSerializable]
public enum TutorialPromptUiKey : byte
{
    Key,
}

/// <summary>
/// Bound UI state for the handheld tutorial prompt window.
/// </summary>
[Serializable, NetSerializable]
public sealed class TutorialPromptBuiState : BoundUserInterfaceState
{
    public string GoalTitle = string.Empty;
    public int GoalIndex;
    public int GoalCount;
    /// <summary>Goal currently shown (may lag behind <see cref="GoalIndex"/> when browsing back).</summary>
    public int ViewGoalIndex;
    public int ViewIndex;
    public int ProgressIndex;
    public int StepCount;
    public string StepText = string.Empty;
    public TutorialStepComplete ViewComplete = TutorialStepComplete.Acknowledge;
    public List<TutorialHudSubGoalState> SubGoalStates = new();
    public bool CanGoBack;
    public bool CanGoNext;
    public bool WaitingOnSensor;
    public bool HasTutorial;
    public string HintText = string.Empty;
    public string StuckHintText = string.Empty;
}

[Serializable, NetSerializable]
public sealed class TutorialPromptNextBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class TutorialPromptHintBuiMsg : BoundUserInterfaceMessage;
