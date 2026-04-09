using Content.Server._NullLink.PlayerData;
using Content.Server._NullLink.Helpers;
using Content.Server.Administration;
using Content.Shared._Starlight.Achievement;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Achievement.Commands;

internal static class AchievementCommandHelpers
{
    public static IEnumerable<string> GetAllProgressKeys(IPrototypeManager prototypeManager)
    {
        var keys = new HashSet<string>();
        foreach (var proto in prototypeManager.EnumeratePrototypes<AchievementPrototype>())
        {
            foreach (var req in proto.Requirements)
                keys.Add(req.ProgressType);
        }
        return keys;
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AchievementUnlockCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public override string Command => "achievement_unlock";
    public override string Description => "Unlocks an achievement for a player.";
    public override string Help => "achievement_unlock <player> <achievementId>";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "player"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<AchievementPrototype>(), "achievementId"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError("Player not found.");
            return;
        }

        var system = _systems.GetEntitySystem<AchievementSystem>();
        system.TryUnlockAchievementAsync(session, args[1])
            .AsTask()
            .FireAndForget();

        shell.WriteLine($"Achievement '{args[1]}' unlocked for {args[0]}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AchievementLockCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public override string Command => "achievement_lock";
    public override string Description => "Locks (revokes) an achievement for a player.";
    public override string Help => "achievement_lock <player> <achievementId>";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "player"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<AchievementPrototype>(), "achievementId"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError("Player not found.");
            return;
        }

        var system = _systems.GetEntitySystem<AchievementSystem>();
        system.TryLockAchievementAsync(session, args[1])
            .AsTask()
            .FireAndForget();

        shell.WriteLine($"Achievement '{args[1]}' locked for {args[0]}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AchievementProgressCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly INullLinkPlayerManager _nullLink = default!;

    public override string Command => "achievement_progress";
    public override string Description => "Shows achievement progress for a player. If no key is specified, shows all progress.";
    public override string Help => "achievement_progress <player> [progressKey]";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "player"),
            2 => CompletionResult.FromHintOptions(AchievementCommandHelpers.GetAllProgressKeys(_prototypeManager), "progressKey"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError("Player not found.");
            return;
        }

        var system = _systems.GetEntitySystem<AchievementSystem>();

        if (args.Length == 2)
        {
            var value = system.GetProgress(session, args[1]);
            var roundValue = system.GetRoundProgress(session.UserId, args[1]);
            shell.WriteLine($"[{args[1]}] total: {value}, round: {roundValue}");
        }
        else
        {
            if (!_nullLink.TryGetPlayerData(session.UserId, out var playerData))
            {
                shell.WriteError("Player data not loaded yet.");
                return;
            }

            if (playerData.AchievementProgress.Count == 0)
            {
                shell.WriteLine("No progress recorded.");
                return;
            }

            foreach (var (key, value) in playerData.AchievementProgress)
            {
                var roundValue = system.GetRoundProgress(session.UserId, key);
                shell.WriteLine($"  [{key}] total: {value}, round: {roundValue}");
            }
        }
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AchievementResetCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override string Command => "achievement_reset";
    public override string Description => "Resets achievement progress for a player. If no key is specified, resets all progress.";
    public override string Help => "achievement_reset <player> [progressKey]";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "player"),
            2 => CompletionResult.FromHintOptions(AchievementCommandHelpers.GetAllProgressKeys(_prototypeManager), "progressKey"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError("Player not found.");
            return;
        }

        var system = _systems.GetEntitySystem<AchievementSystem>();
        var key = args.Length == 2 ? args[1] : null;

        system.ResetProgress(session, key);

        shell.WriteLine(key != null
            ? $"Progress '{key}' reset for {args[0]}."
            : $"All progress reset for {args[0]}.");
    }
}
