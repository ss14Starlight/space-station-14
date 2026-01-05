using Content.Server.Administration;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Station.Components;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class RmStationCommand : LocalizedCommands
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    
    public override string Command => "rmstation";
    public override string Description => "Deletes a station.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError("Not enough arguments.");
            return;
        }

        if (!_entityManager.TryParseNetEntity(args[0], out var station))
        {
            shell.WriteError("Invalid station entity.");
            return;
        }

        var stationSystem = _entitySystemManager.GetEntitySystem<StationSystem>();
        var name = _entityManager.GetComponent<MetaDataComponent>(station.Value).EntityName;
        var uid = station.Value.Id; // dupe
        stationSystem.DeleteStation(station.Value);
        shell.WriteLine($"Deleted station named {name} with id {uid}");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1: return CompletionResult.FromHintOptions(CompletionHelper.Components<StationDataComponent>(args[0], _entityManager), "Station Entities");
        }
        return CompletionResult.Empty;
    }
}