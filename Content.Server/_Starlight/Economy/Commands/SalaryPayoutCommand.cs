using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Economy.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class SalaryPayoutCommand : LocalizedEntityCommands
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SalarySystem _salary = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public override string Command => "salarypayout";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Usage: salarypayout <player>");
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var player))
        {
            shell.WriteError($"Player not found: {args[0]}");
            return;
        }

        var amount = _salary.PaySalary(player);
        shell.WriteLine($"Paid {amount} credits to {player.Name}.");

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{shell.Player?.Name ?? "Console"} paid a salary of {amount} credits to {player.Name}");
    }
}
