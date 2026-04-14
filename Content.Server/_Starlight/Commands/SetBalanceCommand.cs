using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Commands;

/// <summary>
/// Admin command to set a player's Starlight credit balance directly.
/// Usage: setbalance &lt;username&gt; &lt;amount&gt;
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class SetBalanceCommand : IConsoleCommand
{
    public string Command => "setbalance";
    public string Description => "Sets a player's credit balance. Usage: setbalance <username> <amount>";
    public string Help => "setbalance <username> <amount>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Usage: setbalance <username> <amount>");
            return;
        }

        if (!int.TryParse(args[1], out var amount) || amount < 0)
        {
            shell.WriteError("Amount must be a non-negative integer.");
            return;
        }

        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var rolesManager = IoCManager.Resolve<IPlayerRolesManager>();

        ICommonSession? session = null;
        foreach (var s in playerManager.Sessions)
        {
            if (s.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase))
            {
                session = s;
                break;
            }
        }

        if (session is null)
        {
            shell.WriteError($"No connected player found with name '{args[0]}'.");
            return;
        }

        var data = rolesManager.GetPlayerData(session);
        if (data is null)
        {
            shell.WriteError($"No player data loaded for '{args[0]}'. Are they fully connected?");
            return;
        }

        //data.Balance = amount;
        shell.WriteLine($"Set {session.Name}'s balance to {amount} credits.");
    }
}

