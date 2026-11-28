using Content.Shared._Starlight.StationGoals;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.StationGoals;

[RegisterComponent, Access(typeof(StationGoalSystem))]
public sealed partial class StationGoalsComponent : Component
{
    /// <summary>
    /// How many cards are offered at once.
    /// </summary>
    [DataField]
    public int OfferCount = 3;

    /// <summary>
    /// How often the offered cards are redrawn while no vote is running.
    /// </summary>
    [DataField]
    public TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);

    [DataField]
    public int MaxActive = 2;

    /// <summary>
    /// How many departments a new goal may share with each active goal.
    /// </summary>
    [DataField]
    public int MaxSharedDepartments = 1;

    [DataField]
    public TimeSpan VoteDuration = TimeSpan.FromSeconds(90);

    /// <summary>
    /// The department whose living members are counted as voters. The vote closes early once all of them have voted.
    /// </summary>
    [DataField]
    public ProtoId<DepartmentPrototype> VoterDepartment = "Command";

    /// <summary>
    /// Which cargo account gets paid for each department. Departments missing here get no account reward.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DepartmentPrototype>, ProtoId<CargoAccountPrototype>> DepartmentAccounts = [];

    [ViewVariables]
    public List<ProtoId<StationGoalPrototype>> Offers = [];

    [ViewVariables]
    public TimeSpan NextRefresh;

    [ViewVariables]
    public List<ActiveStationGoal> Active = [];

    /// <summary>
    /// Goals that were completed or failed this shift. They are not drawn again.
    /// </summary>
    [ViewVariables]
    public HashSet<ProtoId<StationGoalPrototype>> Finished = [];

    [ViewVariables]
    public StationGoalVote? Vote;
}

public sealed class ActiveStationGoal(ProtoId<StationGoalPrototype> goal, TimeSpan? deadline, int objectives)
{
    [ViewVariables]
    public readonly ProtoId<StationGoalPrototype> Goal = goal;

    [ViewVariables]
    public readonly TimeSpan? Deadline = deadline;

    /// <summary>
    /// Progress of each objective, in the same order as the prototype's objectives.
    /// </summary>
    [ViewVariables]
    public readonly List<StationGoalObjectiveProgress> Progress = CreateProgress(objectives);

    private static List<StationGoalObjectiveProgress> CreateProgress(int count)
    {
        var list = new List<StationGoalObjectiveProgress>(count);
        for (var i = 0; i < count; i++)
            list.Add(new StationGoalObjectiveProgress());
        return list;
    }
}

public sealed class StationGoalObjectiveProgress
{
    [ViewVariables]
    public float Value;

    /// <summary>
    /// Units counted for each prototype, used by sell objectives that need several kinds.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<string, int> Kinds = [];
}

public sealed class StationGoalVote(TimeSpan end, List<ProtoId<StationGoalPrototype>> candidates)
{
    [ViewVariables]
    public readonly TimeSpan End = end;

    [ViewVariables]
    public readonly List<ProtoId<StationGoalPrototype>> Candidates = candidates;

    [ViewVariables]
    public readonly Dictionary<NetUserId, (ProtoId<StationGoalPrototype> Goal, string Name)> Votes = [];
}
