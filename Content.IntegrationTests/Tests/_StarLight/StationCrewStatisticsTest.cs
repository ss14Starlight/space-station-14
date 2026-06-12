#nullable enable
using Content.Server._Starlight.Station;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._Starlight.Station;
using Content.Shared.Maps;
using Content.Shared.StationRecords;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight;

/// <summary>
/// Covers the round-end crew statistics: a crew member whose record entity
/// never leaves the station must not be counted as evacuated.
/// </summary>
[TestFixture]
[TestOf(typeof(StationCrewStatisticsSystem))]
public sealed class StationCrewStatisticsTest
{
    private const string StationMapId = "CrewStatsTestMap";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: gameMap
  id: {StationMapId}
  minPlayers: 0
  mapName: {StationMapId}
  mapPath: /Maps/Test/empty.yml
  stations:
    Station:
      mapNameTemplate: {StationMapId}
      stationProto: StandardNanotrasenStationTestOnly
      components:
      - type: StationCrewStatistics
";

    [Test]
    public async Task CrewOnStationIsNotCountedAsEvacuated()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        // Station map — the crew member lives here.
        var stationMap = await pair.CreateTestMap();
        // Off-station map — used to place an evacuated crew member.
        var offMap = await pair.CreateTestMap();

        var stationSystem = server.System<StationSystem>();
        var recordsSystem = server.System<StationRecordsSystem>();

        var mapProto = server.ProtoMan.Index<GameMapPrototype>(StationMapId);

        var station = EntityUid.Invalid;
        var crewMobOnStation = EntityUid.Invalid;
        var crewMobEvacuated = EntityUid.Invalid;
        var crewMobDeleted = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            station = stationSystem.InitializeNewStation(mapProto.Stations["Station"], null, "Crew Stats Test Station");
            stationSystem.AddGridToStation(station, stationMap.Grid.Owner);

            // Case 1: crew member whose mob is on the station grid the whole round.
            crewMobOnStation = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);

            // Case 2: crew member whose mob is on a different map (evacuated).
            crewMobEvacuated = entMan.SpawnEntity("MobHuman", offMap.GridCoords);

            // Case 3: crew member whose tracked entity will be deleted (lost).
            crewMobDeleted = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);

            var records = entMan.GetComponent<StationRecordsComponent>(station);
            recordsSystem.CreateGeneralRecord(station, null, "On Station Crewman", 30, "Human",
                Gender.Male, "Passenger", null, null, null, records, crewEntity: crewMobOnStation);
            recordsSystem.CreateGeneralRecord(station, null, "Evacuated Crewman", 25, "Human",
                Gender.Female, "Passenger", null, null, null, records, crewEntity: crewMobEvacuated);
            recordsSystem.CreateGeneralRecord(station, null, "Lost Crewman", 35, "Human",
                Gender.Male, "Passenger", null, null, null, records, crewEntity: crewMobDeleted);

            // Delete the third crew member's entity to simulate LostCrew.
            entMan.DeleteEntity(crewMobDeleted);
        });

        await server.WaitAssertion(() =>
        {
            // Precondition: the station data entity is in nullspace — this is why the old
            // Transform(station).MapID comparison was always wrong.
            Assert.That(entMan.GetComponent<TransformComponent>(station).MapID, Is.EqualTo(MapId.Nullspace),
                "Expected the station data entity to live in nullspace.");

            // Trigger the same broadcast event the stats system handles at real round end.
            entMan.EventBus.RaiseEvent(EventSource.Local,
                new GameRunLevelChangedEvent(GameRunLevel.InRound, GameRunLevel.PostRound));

            var stats = entMan.GetComponent<StationCrewStatisticsComponent>(station);
            Assert.Multiple(() =>
            {
                Assert.That(stats.Crew, Is.EqualTo(3), "Expected three registered crew members.");
                Assert.That(stats.LostCrew, Is.EqualTo(1), "Expected one lost crew member (deleted entity).");
                Assert.That(stats.EvacuatedCrew, Is.EqualTo(1), "Expected one evacuated crew member (off-station map).");
            });
        });

        await pair.CleanReturnAsync();
    }
}
