using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks the TutorialServer game rule entity and holds per-player session state.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialServerRuleComponent : Component
{
    /// <summary>
    /// Active / pending tutorial sessions keyed by player.
    /// </summary>
    [DataField]
    public Dictionary<NetUserId, TutorialSessionData> Sessions = new();
}

[DataDefinition, Serializable]
public sealed partial class TutorialSessionData
{
    [DataField]
    public TutorialSessionState State = TutorialSessionState.PendingSelect;

    [DataField]
    public string? SelectedRoleId;

    [DataField]
    public EntityUid MapUid;

    [DataField]
    public EntityUid GridUid;

    [DataField]
    public EntityUid BodyUid;

    [DataField]
    public int StepIndex;

    [DataField]
    public int GoalIndex;

    [DataField]
    public int SubGoalIndex;

    [DataField]
    public bool Completed;

    /// <summary>
    /// Handheld tutorial prompt device given at tutorial start for travel/off-grid roles.
    /// </summary>
    [DataField]
    public EntityUid GuideUid;

    /// <summary>
    /// Soft-following mentor body for single-grid roles (mutually exclusive with <see cref="GuideUid"/>).
    /// </summary>
    [DataField]
    public EntityUid MentorUid;

    /// <summary>
    /// True once the guide Bound UI has been auto-opened for this session
    /// (either at spawn or after the deferred first goal).
    /// </summary>
    [DataField]
    public bool GuideAutoOpened;

    /// <summary>
    /// Player chose Quit on the role picker; do not re-open until they rejoin spawn.
    /// </summary>
    [DataField]
    public bool PickerQuit;

    /// <summary>
    /// Rate-limit for closed-UI progress popups.
    /// </summary>
    [DataField]
    public TimeSpan LastProgressPopup;

    /// <summary>
    /// When true, the player must ReachMarker the chamber entry pad before the goal's YAML sub-goals.
    /// </summary>
    [DataField]
    public bool AwaitingChamberEntryPad;

    /// <summary>
    /// When the current sub-goal became active. Drives
    /// <see cref="TutorialSubGoalData.AutoAdvanceSeconds"/> narration beats.
    /// </summary>
    [DataField]
    public TimeSpan SubGoalStartedAt;

    /// <summary>
    /// Pad the <see cref="TutorialMentorMode.Holopad"/> mentor is currently projected from, so
    /// re-projection only fires when she actually has somewhere new to go.
    /// </summary>
    [DataField]
    public EntityUid MentorHoloPad;

    /// <summary>
    /// Chamber that pad belongs to. Moving between chambers is unconditional; moving between pads
    /// inside one is not.
    /// </summary>
    [DataField]
    public int MentorHoloRoom = -1;

    /// <summary>
    /// Point the <see cref="TutorialMentorMode.Lead"/> mentor is currently walking to or waiting
    /// at, so he is only re-tasked when the curriculum actually has somewhere new to take him.
    /// </summary>
    [DataField]
    public EntityUid MentorWalkPoint;

    /// <summary>
    /// Room that point belongs to. Changing room is what makes the player walk up to him again,
    /// so it is also what clears the coach's arrival gate.
    /// </summary>
    [DataField]
    public int MentorWalkRoom = -1;

    /// <summary>
    /// Control hint for the current sub-goal, held back until the coach has finished her lines so
    /// the banner does not compete with her for the player's attention.
    /// </summary>
    [DataField]
    public string? PendingControlHint;

    /// <summary>
    /// True once <see cref="PendingControlHint"/> has been pushed to the client for this sub-goal.
    /// </summary>
    [DataField]
    public bool ControlHintShown;

    /// <summary>
    /// Last hint echoed into chat, so a beat that banners what the one before it already said does
    /// not say it twice.
    /// </summary>
    [DataField]
    public string? LastChattedHint;
}

public enum TutorialSessionState : byte
{
    PendingSelect,
    InTutorial,
    Exiting,
}
