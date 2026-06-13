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
    public string Description => Loc.GetString("cmd-purchaseshuttle-desc");
    public string Help => Loc.GetString("cmd-purchaseshuttle-help");

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
            shell.WriteError(Loc.GetString("cmd-purchaseshuttle-invalid-integer",
                ("value", args[0])));
            return;
        }

        var shuttlePath = args[1];

        float delay = 1f;
        if (args.Length >= 3 && !float.TryParse(args[2], out delay))
        {
            shell.WriteError(Loc.GetString("cmd-purchaseshuttle-invalid-delay",
                ("value", args[2])));
            return;
        }

        var station = new EntityUid(stationId);
        if (!_entityManager.EntityExists(station))
        {
            shell.WriteError(Loc.GetString("cmd-purchaseshuttle-no-entity",
                ("uid", stationId)));
            return;
        }

        var system = _entitySystemManager.GetEntitySystem<ShipyardSystem>();

        system.PurchaseShuttle(station, shuttlePath, delay, out var vessel);

        if (vessel == null)
        {
            shell.WriteError(Loc.GetString("cmd-purchaseshuttle-failed"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-purchaseshuttle-success",
            ("path", shuttlePath),
            ("station", stationId)));
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
