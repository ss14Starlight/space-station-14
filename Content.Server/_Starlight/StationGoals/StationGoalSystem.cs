using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared._NullLink;
using Content.Shared._Starlight.StationGoals;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.StationGoals;

public sealed partial class StationGoalSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ISharedNullLinkPlayerResourcesManager _playerResources = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private static readonly Color _announcementColor = Color.FromHex("#2ed15a");

    public override void Initialize()
    {
        base.Initialize();
        InitializeConsole();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<StationGoalsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);

            if (comp.Vote is { } vote && now >= vote.End)
                FinishVote(ent);

            if (comp.Vote == null && now >= comp.NextRefresh)
                RefreshOffers((uid, comp));

            for (var i = comp.Active.Count - 1; i >= 0; i--)
            {
                if (comp.Active[i].Deadline is { } deadline && now >= deadline)
                    FailGoal(ent, comp.Active[i]);
            }
        }
    }

    #region Offers

    /// <summary>
    /// Draws a new set of cards from the goals that fit next to the active ones.
    /// </summary>
    public void RefreshOffers(Entity<StationGoalsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var comp = ent.Comp;
        comp.Offers.Clear();
        comp.NextRefresh = _timing.CurTime + comp.RefreshInterval;
        UpdateMarkets((ent, ent.Comp));

        var pool = new List<StationGoalPrototype>();
        foreach (var goal in _proto.EnumeratePrototypes<StationGoalPrototype>())
        {
            if (goal.Offered && goal.Weight > 0 && !comp.Finished.Contains(goal.ID) && IsCompatible(comp, goal))
                pool.Add(goal);
        }

        while (comp.Offers.Count < comp.OfferCount && pool.Count > 0)
        {
            var roll = _random.NextFloat() * pool.Sum(g => g.Weight);
            var index = 0;
            for (; index < pool.Count - 1; index++)
            {
                roll -= pool[index].Weight;
                if (roll <= 0)
                    break;
            }

            comp.Offers.Add(pool[index].ID);
            pool.RemoveAt(index);
        }

        UpdateConsoles(ent);
    }

    /// <summary>
    /// Goal fits when it is not already active and shares few enough departments with every active goal.
    /// </summary>
    public bool IsCompatible(StationGoalsComponent comp, StationGoalPrototype goal)
    {
        foreach (var active in comp.Active)
        {
            if (active.Goal == goal.ID)
                return false;

            if (!_proto.Resolve(active.Goal, out var activeProto))
                continue;

            var shared = goal.Departments.Count(activeProto.Departments.Contains);
            if (shared > comp.MaxSharedDepartments)
                return false;
        }

        return true;
    }

    private bool CanPick(StationGoalsComponent comp, ProtoId<StationGoalPrototype> goal)
        => comp.Active.Count < comp.MaxActive
            && _proto.Resolve(goal, out var proto)
            && IsCompatible(comp, proto);

    #endregion

    #region Vote

    /// <summary>
    /// Opens a command vote over the cards that can be picked right now. Returns false when there is nothing to vote on.
    /// </summary>
    public bool TryStartVote(Entity<StationGoalsComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.Vote != null || comp.Active.Count >= comp.MaxActive)
            return false;

        var candidates = comp.Offers.Where(g => CanPick(comp, g)).ToList();
        if (candidates.Count == 0)
            return false;

        comp.Vote = new StationGoalVote(_timing.CurTime + comp.VoteDuration, candidates);
        UpdateConsoles(ent);
        return true;
    }

    /// <summary>
    /// Records or changes vote. The vote closes early once every living member of the voter department has voted.
    /// </summary>
    public bool TryVote(Entity<StationGoalsComponent> ent, ICommonSession voter, string name, ProtoId<StationGoalPrototype> goal)
    {
        if (ent.Comp.Vote is not { } vote || !vote.Candidates.Contains(goal))
            return false;

        vote.Votes[voter.UserId] = (goal, name);

        if (AllVotersVoted(ent.Comp, vote))
            FinishVote(ent);
        else
            UpdateConsoles(ent);

        return true;
    }

    /// <summary>
    /// Living players whose job belongs to the voter department, at least one.
    /// </summary>
    public int CountVoters(StationGoalsComponent comp)
    {
        var count = 0;
        foreach (var _ in EligibleVoters(comp))
            count++;

        return Math.Max(1, count);
    }

    private bool AllVotersVoted(StationGoalsComponent comp, StationGoalVote vote)
    {
        foreach (var session in EligibleVoters(comp))
        {
            if (!vote.Votes.ContainsKey(session.UserId))
                return false;
        }

        return true;
    }

    private IEnumerable<ICommonSession> EligibleVoters(StationGoalsComponent comp)
    {
        if (!_proto.Resolve(comp.VoterDepartment, out var department))
            yield break;

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } mob || _mobState.IsDead(mob))
                continue;

            if (!_mind.TryGetMind(session, out var mindId, out _)
                || !_jobs.MindTryGetJobId(mindId, out var job)
                || job == null
                || !department.Roles.Contains(job.Value))
                continue;

            yield return session;
        }
    }

    private void FinishVote(Entity<StationGoalsComponent> ent)
    {
        if (ent.Comp.Vote is not { } vote)
            return;

        ent.Comp.Vote = null;

        var tally = vote.Votes.Values
            .GroupBy(v => v.Goal)
            .Select(g => (Goal: g.Key, Count: g.Count()))
            .ToList();

        if (tally.Count == 0)
        {
            UpdateConsoles(ent);
            return;
        }

        var best = tally.Max(t => t.Count);
        var winner = _random.Pick(tally.Where(t => t.Count == best).Select(t => t.Goal).ToList());

        if (!TryActivate(ent, winner))
            UpdateConsoles(ent);
    }

    #endregion

    #region Goals

    /// <summary>
    /// Makes goal active, announces it and redraws the cards.
    /// With force the slot limit and department overlap are ignored.
    /// </summary>
    public bool TryActivate(Entity<StationGoalsComponent> ent, ProtoId<StationGoalPrototype> goalId, bool force = false)
    {
        var comp = ent.Comp;
        if (!_proto.Resolve(goalId, out var goal) || comp.Active.Any(a => a.Goal == goalId))
            return false;

        if (!force && !CanPick(comp, goalId))
            return false;

        var deadline = goal.TimeLimit is { } limit ? _timing.CurTime + limit : (TimeSpan?) null;
        comp.Active.Add(new ActiveStationGoal(goalId, deadline, goal.Objectives.Count));
        comp.Finished.Remove(goalId);

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"Station goal {goalId} activated on {ToPrettyString(ent):station}");

        Announce(ent, Loc.GetString("station-goal-announcement-activated",
            ("name", Loc.GetString(goal.Name)),
            ("description", Loc.GetString(goal.Description)),
            ("departments", DepartmentList(goal))));

        UpdateMarkets(ent);

        var ev = new StationGoalActivatedEvent(ent, goalId);
        RaiseLocalEvent(ref ev);

        RefreshOffers((ent, ent.Comp));
        return true;
    }

    /// <summary>
    /// Reports progress to every active objective that listens on the key.
    /// Counters add the value. Records keep the highest value. With no station every station receives it.
    /// </summary>
    public void ReportProgress(EntityUid? station, string key, float value = 1f)
    {
        var query = EntityQueryEnumerator<StationGoalsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (station != null && station != uid)
                continue;

            var changed = false;
            foreach (var active in comp.Active.ToArray())
            {
                if (!_proto.Resolve(active.Goal, out var goal))
                    continue;

                for (var i = 0; i < goal.Objectives.Count; i++)
                {
                    var progress = active.Progress[i];
                    switch (goal.Objectives[i])
                    {
                        case CounterGoalObjective counter when counter.Key == key:
                            progress.Value += value;
                            changed = true;
                            break;
                        case RecordGoalObjective record when record.Key == key && value > progress.Value:
                            progress.Value = value;
                            changed = true;
                            break;
                    }
                }
            }

            if (changed)
                CheckGoals((uid, comp));
        }
    }

    /// <summary>
    /// Finds the target of an active counter or record objective that listens on the key.
    /// </summary>
    public bool TryGetActiveTarget(EntityUid station, string key, out float target)
    {
        target = 0;
        if (!TryComp<StationGoalsComponent>(station, out var comp))
            return false;

        foreach (var active in comp.Active)
        {
            if (!_proto.Resolve(active.Goal, out var goal))
                continue;

            foreach (var objective in goal.Objectives)
            {
                if ((objective is CounterGoalObjective counter && counter.Key == key)
                    || (objective is RecordGoalObjective record && record.Key == key))
                {
                    target = objective.GetTarget();
                    return true;
                }
            }
        }

        return false;
    }

    private void CheckGoals(Entity<StationGoalsComponent> ent)
    {
        foreach (var active in ent.Comp.Active.ToArray())
        {
            if (_proto.Resolve(active.Goal, out var goal) && IsDone(goal, active))
                CompleteGoal(ent, active);
        }

        UpdateConsoles(ent);
    }

    private static bool IsDone(StationGoalPrototype goal, ActiveStationGoal active)
    {
        for (var i = 0; i < goal.Objectives.Count; i++)
        {
            if (active.Progress[i].Value < goal.Objectives[i].GetTarget())
                return false;
        }

        return true;
    }

    /// <summary>
    /// Ends the goal as done and gives reward.
    /// </summary>
    public void CompleteGoal(Entity<StationGoalsComponent> ent, ActiveStationGoal active)
    {
        if (!ent.Comp.Active.Remove(active) || !_proto.Resolve(active.Goal, out var goal))
            return;

        ent.Comp.Finished.Add(active.Goal);
        UpdateMarkets(ent);

        PayDepartments(ent, goal);
        var paid = PayCrew(goal);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Station goal {goal.ID} completed on {ToPrettyString(ent):station}, {paid} crew members paid");

        Announce(ent, Loc.GetString("station-goal-announcement-completed",
            ("name", Loc.GetString(goal.Name)),
            ("departments", DepartmentList(goal)),
            ("departmentReward", goal.DepartmentReward),
            ("personalReward", goal.PersonalReward)));

        var ev = new StationGoalCompletedEvent(ent, goal.ID);
        RaiseLocalEvent(ref ev);

        UpdateConsoles(ent);
    }

    /// <summary>
    /// Ends the goal without rewards.
    /// </summary>
    public void FailGoal(Entity<StationGoalsComponent> ent, ActiveStationGoal active, bool announce = true)
    {
        if (!ent.Comp.Active.Remove(active))
            return;

        ent.Comp.Finished.Add(active.Goal);
        UpdateMarkets(ent);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"Station goal {active.Goal} failed on {ToPrettyString(ent):station}");

        if (announce && _proto.Resolve(active.Goal, out var goal))
            Announce(ent, Loc.GetString("station-goal-announcement-failed", ("name", Loc.GetString(goal.Name))));

        var ev = new StationGoalFailedEvent(ent, active.Goal);
        RaiseLocalEvent(ref ev);

        UpdateConsoles(ent);
    }

    private void PayDepartments(Entity<StationGoalsComponent> ent, StationGoalPrototype goal)
    {
        if (goal.DepartmentReward <= 0 || !TryComp<StationBankAccountComponent>(ent, out var bank))
            return;

        foreach (var department in goal.Departments)
        {
            if (ent.Comp.DepartmentAccounts.TryGetValue(department, out var account))
                _cargo.UpdateBankAccount((ent, bank), goal.DepartmentReward, account);
        }
    }

    /// <summary>
    /// Pays each living player whose current job belongs to one of the goal's departments.
    /// </summary>
    private int PayCrew(StationGoalPrototype goal)
    {
        if (goal.PersonalReward <= 0)
            return 0;

        var departments = goal.Departments
            .Select(d => _proto.Resolve(d, out var proto) ? proto : null)
            .OfType<DepartmentPrototype>()
            .ToList();

        var paid = 0;
        var name = Loc.GetString(goal.Name);
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } mob || _mobState.IsDead(mob))
                continue;

            if (!_mind.TryGetMind(session, out var mindId, out _)
                || !_jobs.MindTryGetJobId(mindId, out var job)
                || job == null
                || !departments.Any(d => d.Roles.Contains(job.Value)))
                continue;

            if (!_playerResources.TryUpdateResource(session, "credits", goal.PersonalReward))
                continue;

            var message = Loc.GetString("station-goal-reward-message", ("amount", goal.PersonalReward), ("goal", name));
            _chatManager.ChatMessageToOne(ChatChannel.Notifications, message, message, default, false, session.Channel, Color.FromHex("#57A3F7"));
            paid++;
        }

        return paid;
    }

    #endregion

    private void UpdateMarkets(Entity<StationGoalsComponent> ent)
    {
        if (!TryComp<StationCargoOrderDatabaseComponent>(ent, out var db))
            return;

        var wanted = new HashSet<ProtoId<CargoMarketPrototype>>();
        foreach (var active in ent.Comp.Active)
        {
            if (_proto.Resolve(active.Goal, out var goal))
                wanted.UnionWith(goal.CargoMarkets);
        }

        foreach (var goal in _proto.EnumeratePrototypes<StationGoalPrototype>())
        {
            foreach (var market in goal.CargoMarkets)
            {
                if (wanted.Contains(market))
                {
                    if (!db.Markets.Contains(market))
                        db.Markets.Add(market);
                }
                else
                {
                    db.Markets.Remove(market);
                }
            }
        }

        if (wanted.Count == 0)
            return;

        var consoles = EntityQueryEnumerator<CargoOrderConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_station.GetOwningStation(uid) != ent.Owner)
                continue;

            foreach (var market in wanted)
                _cargo.AllowMarket((uid, console), market);
        }
    }

    private void Announce(EntityUid station, string message)
        => _chat.DispatchStationAnnouncement(station, message, Loc.GetString("station-goal-announcement-sender"), colorOverride: _announcementColor);

    private string DepartmentList(StationGoalPrototype goal)
        => string.Join(", ", goal.Departments.Select(d => _proto.Resolve(d, out var proto) ? Loc.GetString(proto.Name) : d.Id));
}

/// <summary>
/// Raised when station picks a goal.
/// </summary>
[ByRefEvent]
public readonly record struct StationGoalActivatedEvent(EntityUid Station, ProtoId<StationGoalPrototype> Goal);

/// <summary>
/// Raised when station completes a goal, after rewards are paid.
/// </summary>
[ByRefEvent]
public readonly record struct StationGoalCompletedEvent(EntityUid Station, ProtoId<StationGoalPrototype> Goal);

/// <summary>
/// Raised when goal ends without being completed, for example when its time runs out or an admin cancels it.
/// </summary>
[ByRefEvent]
public readonly record struct StationGoalFailedEvent(EntityUid Station, ProtoId<StationGoalPrototype> Goal);
