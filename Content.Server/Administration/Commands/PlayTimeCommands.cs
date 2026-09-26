using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Administration;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Moderator)]
public sealed partial class PlayTimeAddOverallCommand : IConsoleCommand
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;

    public string Command => "playtime_addoverall";
    public string Description => Loc.GetString("cmd-playtime_addoverall-desc");
    public string Help => Loc.GetString("cmd-playtime_addoverall-help", ("command", Command));

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("cmd-playtime_addoverall-error-args"));
            return;
        }

        if (!int.TryParse(args[1], out var minutes))
        {
            shell.WriteError(Loc.GetString("parse-minutes-fail", ("minutes", args[1])));
            return;
        }

        // Starlight edit Start
        var overall = await _playTimeTracking.TryAddTimeToOverallPlaytimeByUserName(
            args[0],
            TimeSpan.FromMinutes(minutes));

        if (overall == null)
        // Starlight edit End
        {
            shell.WriteError(Loc.GetString("parse-player-record-fail", ("username", args[0]))); // Starlight Edit: Session -> Record
            return;
        }

        // Starlight edit Start: Removed
        // _playTimeTracking.AddTimeToOverallPlaytime(player, TimeSpan.FromMinutes(minutes));
        // var overall = _playTimeTracking.GetOverallPlaytime(player);
        // Starlight edit End

        shell.WriteLine(Loc.GetString(
            "cmd-playtime_addoverall-succeed",
            ("username", args[0]),
            ("time", overall.Value))); // Starlight Edit: Added .Value
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(),
                Loc.GetString("cmd-playtime_addoverall-arg-user"));

        if (args.Length == 2)
            return CompletionResult.FromHint(Loc.GetString("cmd-playtime_addoverall-arg-minutes"));

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Moderator)]
public sealed partial class PlayTimeAddRoleCommand : IConsoleCommand
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;

    public string Command => "playtime_addrole";
    public string Description => Loc.GetString("cmd-playtime_addrole-desc");
    public string Help => Loc.GetString("cmd-playtime_addrole-help", ("command", Command));

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Loc.GetString("cmd-playtime_addrole-error-args"));
            return;
        }

        var userName = args[0];
        // Starlight edit Start: Removed
        // if (!_playerManager.TryGetSessionByUsername(userName, out var player))
        // {
        //     shell.WriteError(Loc.GetString("parse-session-fail", ("username", userName)));
        //     return;
        // }
        // Starlight edit End

        var role = args[1];

        var m = args[2];
        if (!int.TryParse(m, out var minutes))
        {
            shell.WriteError(Loc.GetString("parse-minutes-fail", ("minutes", minutes)));
            return;
        }
        // Starlight edit Start
        var time = await _playTimeTracking.TryAddTimeToTrackerByUserName(
            userName,
            role,
            TimeSpan.FromMinutes(minutes));

        if (time == null)
        {
            shell.WriteError(Loc.GetString("parse-player-record-fail", ("username", userName)));
            return;
        }
        // Starlight edit End

        // Starlight edit Start: Removed
        // _playTimeTracking.AddTimeToTracker(player, role, TimeSpan.FromMinutes(minutes));
        // var time = _playTimeTracking.GetPlayTimeForTracker(player, role);
        // Starlight edit End
        shell.WriteLine(Loc.GetString("cmd-playtime_addrole-succeed",
            ("username", userName),
            ("role", role),
            ("time", time.Value))); // Starlight Edit: Added .Value
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                Loc.GetString("cmd-playtime_addrole-arg-user"));
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<PlayTimeTrackerPrototype>(),
                Loc.GetString("cmd-playtime_addrole-arg-role"));
        }

        if (args.Length == 3)
            return CompletionResult.FromHint(Loc.GetString("cmd-playtime_addrole-arg-minutes"));

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Moderator)]
public sealed partial class PlayTimeGetOverallCommand : IConsoleCommand
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;

    public string Command => "playtime_getoverall";
    public string Description => Loc.GetString("cmd-playtime_getoverall-desc");
    public string Help => Loc.GetString("cmd-playtime_getoverall-help", ("command", Command));

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("cmd-playtime_getoverall-error-args"));
            return;
        }

        var userName = args[0];
        // Starlight edit Start
        var value = await _playTimeTracking.TryGetPlayTimeForTrackerByUserName(
            userName,
            PlayTimeTrackingShared.TrackerOverall);

        if (value == null)
        // Starlight edit End
        {
            shell.WriteError(Loc.GetString("parse-player-record-fail", ("username", userName))); // Starlight Edit: Session -> Record
            return;
        }

        // var value = _playTimeTracking.GetOverallPlaytime(player); // Starlight Edit: Removed
        shell.WriteLine(Loc.GetString(
            "cmd-playtime_getoverall-success",
            ("username", userName),
            ("time", value.Value))); // Starlight Edit: Added .Value
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                Loc.GetString("cmd-playtime_getoverall-arg-user"));
        }

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Moderator)]
public sealed partial class PlayTimeGetRoleCommand : IConsoleCommand
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;

    public string Command => "playtime_getrole";
    public string Description => Loc.GetString("cmd-playtime_getrole-desc");
    public string Help => Loc.GetString("cmd-playtime_getrole-help", ("command", Command));

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is not (1 or 2))
        {
            shell.WriteLine(Loc.GetString("cmd-playtime_getrole-error-args"));
            return;
        }

        var userName = args[0];
        // Starlight edit Start
        var timers = await _playTimeTracking.TryGetPlayTimesByUserName(userName);

        if (timers == null)
        // Starlight edit End
        {
            shell.WriteError(Loc.GetString("parse-player-record-fail", ("username", userName))); // Starlight Edit: Session -> Record
            return;
        }

        if (args.Length == 1)
        {
            // var timers = _playTimeTracking.GetOriginalTrackerTimes(session); // Starlight Edit: Removed

            if (timers.Count == 0)
            {
                shell.WriteLine(Loc.GetString("cmd-playtime_getrole-no"));
                return;
            }

            foreach (var (role, time) in timers)
            {
                shell.WriteLine(Loc.GetString("cmd-playtime_getrole-role", ("role", role), ("time", time)));
            }
        }

        if (args.Length >= 2)
        {
            if (args[1] == "Overall")
            {
                var timer = timers.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall); // Starlight Edit: _playTimeTracking -> GetValueOrDefault
                shell.WriteLine(Loc.GetString("cmd-playtime_getrole-overall", ("time", timer)));
                return;
            }

            var time = timers.GetValueOrDefault(args[1]); // Starlight Edit: _playTimeTracking -> GetValueOrDefault
            shell.WriteLine(Loc.GetString("cmd-playtime_getrole-succeed", ("username", userName),  // Starlight Edit: session.Name -> userName
                ("time", time)));
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                Loc.GetString("cmd-playtime_getrole-arg-user"));
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<PlayTimeTrackerPrototype>(),
                Loc.GetString("cmd-playtime_getrole-arg-role"));
        }

        return CompletionResult.Empty;
    }
}

/// <summary>
/// Saves the timers for a particular player immediately
/// </summary>
[AdminCommand(AdminFlags.Moderator)]
public sealed partial class PlayTimeSaveCommand : IConsoleCommand
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;

    public string Command => "playtime_save";
    public string Description => Loc.GetString("cmd-playtime_save-desc");
    public string Help => Loc.GetString("cmd-playtime_save-help", ("command", Command));

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Loc.GetString("cmd-playtime_save-error-args"));
            return;
        }

        var name = args[0];
        if (!_playerManager.TryGetSessionByUsername(name, out var pSession))
        {
            shell.WriteError(Loc.GetString("parse-session-fail", ("username", name)));
            return;
        }

        _playTimeTracking.SaveSession(pSession);
        shell.WriteLine(Loc.GetString("cmd-playtime_save-succeed", ("username", name)));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                Loc.GetString("cmd-playtime_save-arg-user"));
        }

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed partial class PlayTimeFlushCommand : IConsoleCommand
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;

    public string Command => "playtime_flush";
    public string Description => Loc.GetString("cmd-playtime_flush-desc");
    public string Help => Loc.GetString("cmd-playtime_flush-help", ("command", Command));

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is not (0 or 1))
        {
            shell.WriteError(Loc.GetString("cmd-playtime_flush-error-args"));
            return;
        }

        if (args.Length == 0)
        {
            _playTimeTracking.FlushAllTrackers();
            return;
        }

        var name = args[0];
        if (!_playerManager.TryGetSessionByUsername(name, out var pSession))
        {
            shell.WriteError(Loc.GetString("parse-session-fail", ("username", name)));
            return;
        }

        _playTimeTracking.FlushTracker(pSession);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                Loc.GetString("cmd-playtime_flush-arg-user"));
        }

        return CompletionResult.Empty;
    }
}
