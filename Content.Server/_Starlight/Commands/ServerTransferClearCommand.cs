using Content.Server.Administration;
using Content.Server._Starlight.ServerTransfer;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed class ServerTransferClearCommand : LocalizedCommands
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    public override string Command => "servertransferclear";
    public override string Description => "Clears the server transfer target. Players will no longer be redirected at round end.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = _entitySystemManager.GetEntitySystem<ServerTransferSystem>();
        var current = system.GetTargetAddress();

        if (string.IsNullOrEmpty(current))
        {
            shell.WriteLine("No server transfer target is currently set.");
            return;
        }

        system.ClearTargetAddress();
        shell.WriteLine($"Server transfer target cleared (was: {current}).");
    }
}
