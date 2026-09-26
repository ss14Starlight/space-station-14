// ReSharper disable CheckNamespace

using System.Threading;
using Content.Server.GameTicking;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared._Starlight.Commands;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Player;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.RoundEnd;

public sealed partial class RoundEndSystem
{
    public void CancelRoundRestartTimer(ICommonSession? canceller = null)
    {
        if (_gameTicker.RunLevel != GameRunLevel.PostRound)
            return;

        if (_countdownTokenSource is null)
            return;

        _countdownTokenSource.Cancel();
        _countdownTokenSource = null;
        _adminLogger.Add(LogType.AdminCommands, LogImpact.High,
            $"Round restart timer was delayed by {CommandHelpers.PlayerNameOrServer(canceller)}");
        _chatManager.SendAdminAnnouncement(
            $"Round restart timer was delayed by {CommandHelpers.PlayerNameOrServer(canceller)}");
    }

    public void StartRestartTimer(TimeSpan? countdownTime = null)
    {
        _countdownTokenSource?.Cancel();
        _countdownTokenSource = new CancellationTokenSource();

        countdownTime ??= TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.RoundRestartTime));
        int time;
        string unitsLocString;
        if (countdownTime.Value.TotalDays >= 1)
        {
            time = (int)countdownTime.Value.TotalDays;
            unitsLocString = "eta-units-days";
        }
        else if (countdownTime.Value.TotalHours >= 1)
        {
            time = (int)countdownTime.Value.TotalHours;
            unitsLocString = "eta-units-hours";
        }
        else if (countdownTime.Value.TotalMinutes >= 1)
        {
            time = (int)countdownTime.Value.TotalMinutes;
            unitsLocString = "eta-units-minutes";
        }
        else
        {
            time = (int)countdownTime.Value.TotalSeconds;
            unitsLocString = "eta-units-seconds";
        }

        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(
                "round-end-system-round-restart-eta-announcement",
                ("time", time),
                ("units", Loc.GetString(unitsLocString))));
        Timer.Spawn(countdownTime.Value, AfterEndRoundRestart, _countdownTokenSource.Token);
    }

    public bool IsRestartTimerActive() =>
        _gameTicker.RunLevel == GameRunLevel.PostRound && _countdownTokenSource is not null;

    private void StartCallVote()
    {
        var options = new VoteOptions()
        {
            DisplayVotes = false,
            Duration = TimeSpan.FromSeconds(30),
            VoterEligibility = VoteManager.VoterEligibility.NonAntag,
            Title = Loc.GetString("round-end-system-shuttle-auto-called-call-vote")
        };
        options.SetInitiatorOrServer(null);
        options.Options.Add(("Yes", 0));
        options.Options.Add(("No", 1));

        var vote = _voteManager.CreateVote(options);
        vote.OnFinished += (_, args) =>
        {
            if (args.Winner == null)
            {
                RequestRoundEnd(null, null, false, "round-end-system-shuttle-auto-called-announcement");
                return;
            }

            if ((int)args.Winner == 0)
            {
                RequestRoundEnd(null, null, false, "round-end-system-shuttle-auto-called-announcement");
            }
            else
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("round-end-system-shuttle-auto-vote-result-no",
                    ("minutes", _cfg.GetCVar(CCVars.EmergencyShuttleAutoCallExtensionTime))));
            }
        };
    }

    public void ToggleTimerOnEnd(bool state, ICommonSession? session = null)
    {
        StartTimerOnRestart = state;
        _adminLogger.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{CommandHelpers.PlayerNameOrServer(session)} toggled {(state ? "on" : "off")} the restart timer on round end.");
    }

    public void DelayShuttleTimer(float seconds)
    {
        if (!IsRoundEndRequested()) return; // Don't reset if its not even called rn
        if (_shuttle.EmergencyShuttleArrived || _shuttle.ShuttlesLeft) return; // Don't reset it if it already arrived
        if (_gameTicker.RunLevel != GameRunLevel.InRound) return; // DEFINITELY do not reset it here godo

        _countdownTokenSource?.Cancel();
        _countdownTokenSource = new CancellationTokenSource();

        ExpectedCountdownEnd += TimeSpan.FromSeconds(seconds);
        var timeLeft = ExpectedCountdownEnd!.Value - _gameTiming.CurTime;

        Timer.Spawn(timeLeft, _shuttle.DockEmergencyShuttle, _countdownTokenSource.Token);
    }
}
