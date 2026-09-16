using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Economy.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class SalaryPayoutCommand : LocalizedEntityCommands
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SalarySystem _salary = default!;

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
    }
}
