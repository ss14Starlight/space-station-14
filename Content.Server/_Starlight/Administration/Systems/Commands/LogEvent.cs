using Content.Server._Starlight.Administration.Systems;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class LogEventCommand : LocalizedEntityCommands
{
    [Dependency] private readonly AutoDiscordLogSystem _autolog = default!;

    public override string Command => "logevent";
    public override string Description => "Quickly log something on the discord, this can be an event, admeme, ect...";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length < 1)
            return;

        var message = string.Join(" ", args).Trim();
        if (string.IsNullOrEmpty(message))
            return;

        _autolog.LogToDiscord(message, shell.Player!.Name);
    }
}