using Content.Server.Administration;
using Content.Server._Starlight.ServerTransfer;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed class ServerTransferCommand : LocalizedCommands
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    public override string Command => "servertransfer";
    public override string Description => "Sets a target server address (ss14://host:port) to redirect all players to at round end.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Usage: servertransfer ss14://host:port");
            return;
        }

        var address = args[0];

        if (!address.StartsWith("ss14://", StringComparison.OrdinalIgnoreCase))
        {
            shell.WriteError("Address must start with ss14://");
            return;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out _))
        {
            shell.WriteError("Invalid URI format.");
            return;
        }

        var system = _entitySystemManager.GetEntitySystem<ServerTransferSystem>();
        system.SetTargetAddress(address);
        shell.WriteLine($"Server transfer target set to: {address}");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHint("ss14://host:port");
        }
        return CompletionResult.Empty;
    }
}
