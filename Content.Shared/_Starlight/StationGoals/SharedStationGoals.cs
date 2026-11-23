using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.StationGoals;

[Serializable, NetSerializable]
public enum StationGoalConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class StationGoalConsoleState : BoundUserInterfaceState
{
    public List<StationGoalOfferState> Offers = new();
    public List<StationGoalActiveState> Active = new();

    /// <summary>
    /// Game time when the offered cards are redrawn.
    /// </summary>
    public TimeSpan NextRefresh;

    /// <summary>
    /// Game time when the running vote closes, or null when nobody is voting.
    /// </summary>
    public TimeSpan? VoteEnd;

    public int VotesCast;
    public int Voters;
    public int MaxActive;
    public bool HasStation;
}

[Serializable, NetSerializable]
public sealed class StationGoalOfferState
{
    public ProtoId<StationGoalPrototype> Goal;

    /// <summary>
    /// False when the goal shares too many departments with an active one, or all goal slots are taken.
    /// </summary>
    public bool Available;

    /// <summary>
    /// Names of the command members who voted for this card in the running vote.
    /// </summary>
    public List<string> Voters = [];
}

[Serializable, NetSerializable]
public sealed class StationGoalActiveState
{
    public ProtoId<StationGoalPrototype> Goal;
    public TimeSpan? Deadline;
    public List<StationGoalObjectiveState> Objectives = [];
}

[Serializable, NetSerializable]
public sealed class StationGoalObjectiveState
{
    public string Text = string.Empty;
    public float Current;
    public float Target;
}

[Serializable, NetSerializable]
public sealed class StationGoalStartVoteMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class StationGoalVoteMessage(ProtoId<StationGoalPrototype> goal) : BoundUserInterfaceMessage
{
    public readonly ProtoId<StationGoalPrototype> Goal = goal;
}

[Serializable, NetSerializable]
public sealed class StationGoalPrintMessage : BoundUserInterfaceMessage;
