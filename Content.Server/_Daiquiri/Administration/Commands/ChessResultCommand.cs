using System;
using System.Collections.Generic;
using Content.Server.Administration;
using Content.Server._Daiquiri.CartridgeLoader.Cartridges;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server._Daiquiri.Administration.Commands;

/// <summary>
/// Temporary manual entry point for the chess result webhook. The Lichess Board API
/// bridge should call <see cref="ChessCartridgeSystem.ReportGameResult"/> automatically
/// once it is implemented; this command lets admins verify the Discord payload meanwhile.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class ChessResultCommand : LocalizedCommands
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEntitySystemManager _systems = default!;

    public override string Command => "chessresult";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 or 2 => CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(true, _player),
                "username"),
            3 => CompletionResult.FromHintOptions(
                new[] { "white", "black", "draw" },
                "outcome"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteLine("Usage: chessresult <whiteUsername> <blackUsername> <white|black|draw> [gameUrl]");
            return;
        }

        if (!_player.TryGetSessionByUsername(args[0], out var white))
        {
            shell.WriteError($"No player found with username '{args[0]}'.");
            return;
        }

        if (!_player.TryGetSessionByUsername(args[1], out var black))
        {
            shell.WriteError($"No player found with username '{args[1]}'.");
            return;
        }

        if (white.UserId == black.UserId)
        {
            shell.WriteError("White and black must be different players.");
            return;
        }

        ChessGameOutcome outcome;
        switch (args[2].ToLowerInvariant())
        {
            case "white":
            case "1-0":
                outcome = ChessGameOutcome.WhiteWin;
                break;
            case "black":
            case "0-1":
                outcome = ChessGameOutcome.BlackWin;
                break;
            case "draw":
            case "0.5-0.5":
            case "1/2-1/2":
                outcome = ChessGameOutcome.Draw;
                break;
            default:
                shell.WriteError("Outcome must be one of: white, black, draw.");
                return;
        }

        var gameUrl = args.Length > 3 ? args[3] : null;
        var chess = _systems.GetEntitySystem<ChessCartridgeSystem>();
        var whiteResult = chess.GetPlayerResult(white);
        var blackResult = chess.GetPlayerResult(black);

        // Manual admin testing is not a public Lichess seek, so it must not change
        // the leaderboard counters.
        chess.ReportGameResult(whiteResult, blackResult, outcome, gameUrl, leaderboard: false);

        if (chess.HasResultWebhook)
        {
            shell.WriteLine(
                $"Reported {outcome} for {white.Name} vs {black.Name} to the chess webhook.");
        }
        else
        {
            shell.WriteError(
                "The chess result webhook is not configured (discord.chess_webhook is empty).");
        }
    }
}
