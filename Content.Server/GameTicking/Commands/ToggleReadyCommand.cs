using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.GameTicking.Commands;

[AnyCommand]
public sealed class ToggleReadyCommand : LocalizedEntityCommands
{
    [Dependency] private readonly GameTicker _gameTicker = default!;

    public override string Command => "toggleready";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            return;
        }

        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-only-players-can-run-this-command"));
            return;
        }

        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
        {
            // Starlight - due to synchronization issues, we can get this from the client
            // immediately after we leave the lobby. This is more of an issue for us than
            // for upstream because we have more states where this can get changed by the
            // menu automatically, and that can cause test failures.
            //shell.WriteError(Loc.GetString("shell-can-only-run-from-pre-round-lobby"));
            return;
        }

        if (!bool.TryParse(args[0], out var ready))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-boolean"));
            return;
        }

        _gameTicker.ToggleReady(player, ready);
    }
}
