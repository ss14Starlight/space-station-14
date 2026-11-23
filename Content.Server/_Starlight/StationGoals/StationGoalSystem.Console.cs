using System.Text;
using Content.Server.Popups;
using Content.Shared._Starlight.StationGoals;
using Content.Shared.Access.Systems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Starlight.StationGoals;

public sealed partial class StationGoalSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    private void InitializeConsole()
        => Subs.BuiEvents<StationGoalConsoleComponent>(StationGoalConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<StationGoalStartVoteMessage>(OnStartVote);
            subs.Event<StationGoalVoteMessage>(OnVote);
            subs.Event<StationGoalPrintMessage>(OnPrint);
        });

    private void OnConsoleOpened(Entity<StationGoalConsoleComponent> ent, ref BoundUIOpenedEvent args)
        => UpdateConsole(ent);

    private void OnStartVote(Entity<StationGoalConsoleComponent> ent, ref StationGoalStartVoteMessage args)
    {
        if (!CheckAccess(ent, args.Actor) || !TryGetStation(ent, out var station))
            return;

        if (!TryStartVote(station))
        {
            _popup.PopupEntity(Loc.GetString("station-goal-console-vote-unavailable"), ent, args.Actor);
            return;
        }

        _adminLog.Add(Shared.Database.LogType.Action, Shared.Database.LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} started a station goal vote on {ToPrettyString(station):station}");
    }

    private void OnVote(Entity<StationGoalConsoleComponent> ent, ref StationGoalVoteMessage args)
    {
        if (!CheckAccess(ent, args.Actor)
            || !TryGetStation(ent, out var station)
            || !TryComp<ActorComponent>(args.Actor, out var actor))
            return;

        TryVote(station, actor.PlayerSession, Name(args.Actor), args.Goal);
    }

    private void OnPrint(Entity<StationGoalConsoleComponent> ent, ref StationGoalPrintMessage args)
    {
        if (!CheckAccess(ent, args.Actor) || !TryGetStation(ent, out var station))
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextPrint)
            return;

        ent.Comp.NextPrint = now + ent.Comp.PrintCooldown;

        var paper = SpawnAtPosition(ent.Comp.PaperPrototype, Transform(ent).Coordinates);
        _metaData.SetEntityName(paper, Loc.GetString("station-goal-paper-name"));
        _paper.SetContent(paper, BuildSheet(station));
        _audio.PlayPvs(ent.Comp.PrintSound, ent);
    }

    private bool CheckAccess(EntityUid console, EntityUid user)
    {
        if (_access.IsAllowed(user, console))
            return true;

        _popup.PopupEntity(Loc.GetString("station-goal-console-access-denied"), console, user, PopupType.SmallCaution);
        return false;
    }

    private bool TryGetStation(EntityUid console, out Entity<StationGoalsComponent> station)
    {
        station = default;
        if (_station.GetOwningStation(console) is not { } uid || !TryComp<StationGoalsComponent>(uid, out var comp))
            return false;

        station = (uid, comp);
        return true;
    }

    /// <summary>
    /// Pushes the current state to every open goal console on the station.
    /// </summary>
    public void UpdateConsoles(EntityUid station)
    {
        var query = EntityQueryEnumerator<StationGoalConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_station.GetOwningStation(uid) == station)
                UpdateConsole((uid, comp));
        }
    }

    private void UpdateConsole(Entity<StationGoalConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, StationGoalConsoleUiKey.Key))
            return;

        var state = new StationGoalConsoleState();
        if (TryGetStation(ent, out var station))
            FillState(station, state);

        _ui.SetUiState(ent.Owner, StationGoalConsoleUiKey.Key, state);
    }

    private void FillState(Entity<StationGoalsComponent> station, StationGoalConsoleState state)
    {
        var comp = station.Comp;
        state.HasStation = true;
        state.NextRefresh = comp.NextRefresh;
        state.MaxActive = comp.MaxActive;

        foreach (var goal in comp.Offers)
        {
            var offer = new StationGoalOfferState
            {
                Goal = goal,
                Available = comp.Vote?.Candidates.Contains(goal) ?? CanPick(comp, goal),
            };

            if (comp.Vote != null)
            {
                foreach (var (_, vote) in comp.Vote.Votes)
                {
                    if (vote.Goal == goal)
                        offer.Voters.Add(vote.Name);
                }
            }

            state.Offers.Add(offer);
        }

        foreach (var active in comp.Active)
        {
            if (!_proto.Resolve(active.Goal, out var goal))
                continue;

            var activeState = new StationGoalActiveState { Goal = active.Goal, Deadline = active.Deadline };
            for (var i = 0; i < goal.Objectives.Count; i++)
            {
                activeState.Objectives.Add(new StationGoalObjectiveState
                {
                    Text = goal.Objectives[i].GetText(),
                    Current = active.Progress[i].Value,
                    Target = goal.Objectives[i].GetTarget(),
                });
            }

            state.Active.Add(activeState);
        }

        if (comp.Vote != null)
        {
            state.VoteEnd = comp.Vote.End;
            state.VotesCast = comp.Vote.Votes.Count;
            state.Voters = CountVoters(comp);
        }
    }

    private string BuildSheet(Entity<StationGoalsComponent> station)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("station-goal-paper-header", ("station", Name(station))));
        sb.AppendLine();

        if (station.Comp.Active.Count == 0)
        {
            sb.AppendLine(Loc.GetString("station-goal-paper-none"));
            return sb.ToString();
        }

        foreach (var active in station.Comp.Active)
        {
            if (!_proto.Resolve(active.Goal, out var goal))
                continue;

            sb.AppendLine(Loc.GetString("station-goal-paper-goal", ("name", Loc.GetString(goal.Name))));
            sb.AppendLine(Loc.GetString("station-goal-paper-departments", ("departments", DepartmentList(goal))));
            sb.AppendLine(Loc.GetString(goal.Description));

            if (goal.Instructions is { } instructions)
            {
                sb.AppendLine();
                sb.AppendLine(Loc.GetString(instructions));
            }

            sb.AppendLine();
            sb.AppendLine(Loc.GetString("station-goal-paper-objectives"));
            for (var i = 0; i < goal.Objectives.Count; i++)
            {
                sb.AppendLine(Loc.GetString("station-goal-paper-objective",
                    ("text", goal.Objectives[i].GetText()),
                    ("current", MathF.Floor(active.Progress[i].Value)),
                    ("target", goal.Objectives[i].GetTarget())));
            }

            if (active.Deadline is { } deadline)
            {
                var left = deadline - _timing.CurTime;
                sb.AppendLine(Loc.GetString("station-goal-paper-deadline", ("minutes", Math.Max(0, (int) left.TotalMinutes))));
            }

            sb.AppendLine(Loc.GetString("station-goal-paper-rewards",
                ("departmentReward", goal.DepartmentReward),
                ("personalReward", goal.PersonalReward)));
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
