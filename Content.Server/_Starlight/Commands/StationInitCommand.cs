using System.Linq;
using Content.Server._Starlight.Station;
using Content.Server.Administration;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Station.Components;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class StationInitCommand : LocalizedCommands
{
    // syntax: stationinit GRID_ID STATION_PROTO_ID TARGET_STATION_ID
    
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    
    public override string Command => "stationinit";
    public override string Description => "Turns a grid into a new or existing station.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(
                "Initializes a grid as a station. Check /Resources/Prototypes/Entities/Stations for valid station prototypes.");
            shell.WriteLine("Syntax: stationinit GRID_ID STATION_PROTO_ID <TARGET_STATION_ID>");
            return;
        }

        var stationSystem = _entitySystemManager.GetEntitySystem<StationSystem>();
        if (!_entityManager.TryParseNetEntity(args[0], out var grid))
        {
            if (args[0] == "all")
            {
                var query = _entityManager.EntityQueryEnumerator<BecomesStationMidRoundComponent>();
                var added = new List<EntityUid>();
                while (query.MoveNext(out var uid, out var comp))
                {
                    if (_entityManager.HasComponent<StationMemberComponent>(uid)) continue;
                    if (comp.BaseStationProtos is null || comp.BaseStationProtos.Count == 0) continue;
                    stationSystem.MarkMidRoundStationForInitialization(uid, comp);
                    var id = stationSystem.InitializeNewStationMidRound(uid, comp.BaseStationProtos, comp);
                    added.Add(id);
                }

                if (added.Count == 0)
                {
                    shell.WriteLine("No stations initialized.");
                    return;
                }
                shell.WriteLine($"Initialized stations with the following IDs: {string.Join(", ", added)}.");
                return;
            }
            shell.WriteError("Invalid grid entity ID.");
            return;
        }

        if (_entityManager.TryGetComponent(grid, out StationMemberComponent? stationMemberComponent))
        {
            shell.WriteError("This grid already belongs to a station!");
            return;
        }

        if (args.Length == 1)
        {
            if (!_entityManager.TryGetComponent<BecomesStationMidRoundComponent>(grid, out var midround) ||
                midround.BaseStationProtos is null || midround.BaseStationProtos.Count == 0)
            {
                stationSystem.InitializeNewStationMidRound(grid.Value, "StandardNanotrasenStation");
                shell.WriteLine($"Station with ID {grid.Value} initialized. Used StandardNanotrasenStation as default prototype.");
                return;
            }
            stationSystem.MarkMidRoundStationForInitialization(grid.Value, midround);
            var id = stationSystem.InitializeNewStationMidRound(grid.Value, midround.BaseStationProtos, midround);
            shell.WriteLine($"Station with ID {id} initialized!");
            return;
        }
        
        if (!_prototypeManager.TryIndex(args[1], out var prototype))
        {
            shell.WriteError(
                "Invalid station prototype ID. Check /Resources/Prototypes/Entities/Stations for valid station prototypes.");
            return;
        }

        if (args.Length == 3)
        {
            if (!_entityManager.TryParseNetEntity(args[3], out var station))
            {
                shell.WriteError("Invalid target station ID, doesn't exist or isn't a station.");
                return;
            }

            var data = _entityManager.GetComponent<StationDataComponent>(station.Value);
            var name = _entityManager.GetComponent<MetaDataComponent>(station.Value).EntityName;
            
            stationSystem.AddGridToStation(station.Value, grid.Value, null, data, name);
            shell.WriteLine($"Added grid with ID ${grid.Value} to station with ID ${station.Value}!");
        }
        else
        {
            var id = stationSystem.InitializeNewStationMidRound(grid.Value, prototype);
            shell.WriteLine($"Station with ID {id} initialized!");
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHintOptions(CompletionHelper.Components<MapGridComponent>(args[0], _entityManager).Append(new CompletionOption("all")), "Grid ID or all");
            case 3:
                return CompletionResult.FromHintOptions(CompletionHelper.Components<StationDataComponent>(args[2], _entityManager), "Station ID");
        }
        return CompletionResult.Empty;
    }
}