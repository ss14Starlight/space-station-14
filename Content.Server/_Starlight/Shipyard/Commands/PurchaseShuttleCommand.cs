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
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    public string Command => "purchaseshuttle";
    public string Description => "Spawns and docks a specified shuttle from a grid file";
    public string Help => $"{Command} <station ID> <gridfile path>";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 2), ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        if (!int.TryParse(args[0], out var stationId))
        {
            shell.WriteError($"{args[0]} is not a valid integer.");
            return;
        }

        var shuttlePath = args[1];

        float delay = 1f;

        var station = new EntityUid(stationId);
        if (!_entityManager.EntityExists(station))
        {
            shell.WriteError($"No entity with UID {stationId} exists.");
            return;
        }

        var system = _entitySystemManager.GetEntitySystem<ShipyardSystem>();

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
