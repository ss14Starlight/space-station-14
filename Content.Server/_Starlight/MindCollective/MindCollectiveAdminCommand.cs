using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.MindCollective;

[AdminCommand(AdminFlags.Debug)]
public sealed class MindCollectiveAdminCommand : LocalizedEntityCommands
{
    [Dependency] private MindCollectiveSystem _collectives = default!;

    public override string Command => "mindcollective";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is { } player && args.Length == 0)
            _collectives.OpenAdminUi(player);
    }
}
