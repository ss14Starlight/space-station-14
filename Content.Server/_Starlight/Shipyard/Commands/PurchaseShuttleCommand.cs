using Content.Server.Administration;
using Content.Shared.Maps;
using Content.Server._Starlight.Shipyard.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Shipyard.Commands;

/// <summary>
/// Purchases a shuttle and docks it to a station.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class PurchaseShuttleCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _entityManager = default!;
    public string Command => "purchaseshuttle";
    public string Description => "Spawns and docks a specified shuttle from a grid file";
    public string Help => $"{Command} <station ID> <gridfile path>";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError($"Usage: {Help}");
            return;
        }

        if (!int.TryParse(args[0], out var stationId))
        {
            shell.WriteError($"{args[0]} is not a valid integer.");
            return;
        }

        var shuttlePath = args[1];

        float delay = 1f;
        if (args.Length >= 3 && !float.TryParse(args[2], out delay))
        {
            shell.WriteError($"{args[2]} is not a valid delay.");
            return;
        }

        var system = _entityManager.GetEntitySystem<ShipyardSystem>();
        var station = new EntityUid(stationId);

        system.PurchaseShuttle(station, shuttlePath, delay, out _);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHint(Loc.GetString("station-id"));
            case 2:
                var opts = CompletionHelper.PrototypeIDs<GameMapPrototype>();
                return CompletionResult.FromHintOptions(opts, Loc.GetString("cmd-hint-savemap-path"));
        }

        return CompletionResult.Empty;
    }
}
