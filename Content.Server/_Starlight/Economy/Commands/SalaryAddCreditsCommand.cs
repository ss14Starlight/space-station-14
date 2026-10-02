using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared._NullLink;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Economy.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class SalaryAddCreditsCommand : LocalizedEntityCommands
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private ISharedNullLinkPlayerResourcesManager _resources = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public override string Command => "salarychangecredits";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 || !int.TryParse(args[1], out var amount))
        {
            shell.WriteError("Usage: salarychangecredits <player> <amount>");
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var player))
        {
            shell.WriteError($"Player not found: {args[0]}");
            return;
        }

        if (!_resources.TryUpdateResource(player, "credits", amount))
        {
            shell.WriteError($"Could not update credits for {player.Name}.");
            return;
        }

        shell.WriteLine($"Changed credits for {player.Name} by {amount}.");

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{shell.Player?.Name ?? "Console"} changed credits for {player.Name} by {amount}");
    }
}
