#nullable enable
using System.Linq;
using Content.Server._Starlight.Station;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._Starlight.Railroading;
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

    // Job prototype IDs verified to exist in Resources/Prototypes/Roles/Jobs/Science/borg.yml
    private const string BorgJobId = "Borg";
    private const string StationAiJobId = "StationAi";
    private const string PassengerJobId = "Passenger";

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
        // Off-station map — holds a station member grid that left, like the
        // emergency shuttle at CentComm.
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
            stationSystem.AddMainGridToStation(station, stationMap.Grid.Owner);
            // A plain member grid that ended up on another map, like a departed shuttle.
            // Crew on it must count as evacuated: the comparison uses the main grids.
            stationSystem.AddGridToStation(station, offMap.Grid.Owner);

            // Case 1: crew member whose mob is on the main station grid the whole round.
            crewMobOnStation = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);

            // Case 2: crew member whose mob left on the member grid (evacuated).
            crewMobEvacuated = entMan.SpawnEntity("MobHuman", offMap.GridCoords);

            // Case 3: crew member whose tracked entity will be deleted (lost).
            crewMobDeleted = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);

            var records = entMan.GetComponent<StationRecordsComponent>(station);
            recordsSystem.CreateGeneralRecord(station, null, "On Station Crewman", 30, "Human",
                Gender.Male, PassengerJobId, null, null, null, records, crewEntity: crewMobOnStation);
            recordsSystem.CreateGeneralRecord(station, null, "Evacuated Crewman", 25, "Human",
                Gender.Female, PassengerJobId, null, null, null, records, crewEntity: crewMobEvacuated);
            recordsSystem.CreateGeneralRecord(station, null, "Lost Crewman", 35, "Human",
                Gender.Male, PassengerJobId, null, null, null, records, crewEntity: crewMobDeleted);

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

    /// <summary>
    /// TEST B — respawn reuse: when CreateGeneralRecord is called twice with the same name
    /// the record's Entity field must be updated to the new mob, not left pointing at the old one.
    /// Bug shape: respawned players were counted dead/lost because the record kept the old body.
    /// This test fails if the "Starlight - Start: track the new body on respawn" block in
    /// StationRecordsSystem.CreateGeneralRecord is removed.
    /// </summary>
    [Test]
    public async Task RespawnReusePoinststRecordAtNewMob()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var stationMap = await pair.CreateTestMap();

        var stationSystem = server.System<StationSystem>();
        var recordsSystem = server.System<StationRecordsSystem>();
        var sharedRecords = server.System<SharedStationRecordsSystem>();

        var mapProto = server.ProtoMan.Index<GameMapPrototype>(StationMapId);

        var station = EntityUid.Invalid;
        var mobA = EntityUid.Invalid;
        var mobB = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            station = stationSystem.InitializeNewStation(mapProto.Stations["Station"], null, "Respawn Test Station");
            stationSystem.AddMainGridToStation(station, stationMap.Grid.Owner);

            mobA = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);
            mobB = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);

            var records = entMan.GetComponent<StationRecordsComponent>(station);

            // First spawn — creates the record tracking mobA.
            recordsSystem.CreateGeneralRecord(station, null, "Respawn Crewman", 30, "Human",
                Gender.Male, PassengerJobId, null, null, null, records, crewEntity: mobA);

            // Second spawn with the same name — simulates respawn; must repoint to mobB.
            recordsSystem.CreateGeneralRecord(station, null, "Respawn Crewman", 30, "Human",
                Gender.Male, PassengerJobId, null, null, null, records, crewEntity: mobB);
        });

        await server.WaitAssertion(() =>
        {
            // Exactly one record must exist for this name.
            var allRecords = sharedRecords.GetRecordsOfType<GeneralStationRecord>(station)
                .Where(r => r.Item2.Name == "Respawn Crewman")
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(allRecords, Has.Count.EqualTo(1),
                    "Expected exactly one record for 'Respawn Crewman' — respawn must reuse the existing entry.");

                // The record's Entity must point at mobB, not mobA.
                // This assertion breaks if the respawn-repoint block is removed.
                Assert.That(allRecords[0].Item2.Entity, Is.EqualTo(entMan.GetNetEntity(mobB)),
                    "Expected the record's Entity to be updated to the new respawn mob.");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// TEST C — borg and StationAi classification.
    /// Borgs on the station grid → Borgs++; borgs off it → StolenBorgs++.
    /// StationAi records contribute to none of the counters (skipped by job.ID check).
    /// This test fails if the "if (job.ID == StationAi) continue" or "isBorg = job.ID == Borg"
    /// branches in StationCrewStatisticsSystem.CheckStation are changed.
    /// </summary>
    [Test]
    public async Task BorgAndStationAiClassification()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var stationMap = await pair.CreateTestMap();
        var offMap = await pair.CreateTestMap();

        var stationSystem = server.System<StationSystem>();
        var recordsSystem = server.System<StationRecordsSystem>();

        var mapProto = server.ProtoMan.Index<GameMapPrototype>(StationMapId);

        var station = EntityUid.Invalid;
        var borgOnStation = EntityUid.Invalid;
        var borgOffStation = EntityUid.Invalid;
        var aiOnStation = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            station = stationSystem.InitializeNewStation(mapProto.Stations["Station"], null, "Borg Test Station");
            stationSystem.AddMainGridToStation(station, stationMap.Grid.Owner);
            stationSystem.AddGridToStation(station, offMap.Grid.Owner);

            borgOnStation = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);
            borgOffStation = entMan.SpawnEntity("MobHuman", offMap.GridCoords);
            aiOnStation = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);

            var records = entMan.GetComponent<StationRecordsComponent>(station);

            recordsSystem.CreateGeneralRecord(station, null, "Borg On Station", 1, "Machine",
                Gender.Epicene, BorgJobId, null, null, null, records, crewEntity: borgOnStation);
            recordsSystem.CreateGeneralRecord(station, null, "Borg Off Station", 1, "Machine",
                Gender.Epicene, BorgJobId, null, null, null, records, crewEntity: borgOffStation);
            recordsSystem.CreateGeneralRecord(station, null, "Station AI", 1, "Machine",
                Gender.Epicene, StationAiJobId, null, null, null, records, crewEntity: aiOnStation);
        });

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseEvent(EventSource.Local,
                new GameRunLevelChangedEvent(GameRunLevel.InRound, GameRunLevel.PostRound));

            var stats = entMan.GetComponent<StationCrewStatisticsComponent>(station);
            Assert.Multiple(() =>
            {
                // Two borgs registered total (StationAi skipped — it never increments Borgs).
                Assert.That(stats.Borgs, Is.EqualTo(2),
                    "Expected exactly 2 borgs (StationAi must not be counted).");
                // The borg on the off-station map is stolen.
                Assert.That(stats.StolenBorgs, Is.EqualTo(1),
                    "Expected 1 stolen borg (the one on the off-station map).");
                // No borgs deleted.
                Assert.That(stats.LostBorgs, Is.EqualTo(0),
                    "Expected 0 lost borgs.");
                // StationAi must not affect the human crew counter.
                Assert.That(stats.Crew, Is.EqualTo(0),
                    "Expected 0 human crew (StationAi must not inflate Crew).");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// TEST D — crew retention task end-to-end ordering.
    /// Sets up 3 crew records (1 on-station, 1 evacuated, 1 deleted) plus a performer entity
    /// carrying RailroadCrewRetentionTaskComponent and RailroadCardPerformerComponent.
    /// After a single PostRound event, the task's Progress must be 0.5 (1 evacuated / 2 alive).
    /// This test fails if the `after: [typeof(StationCrewStatisticsSystem)]` ordering constraint
    /// is removed from RailroadingCrewRetentionTaskSystem, because the task system would then
    /// read stale (zeroed) statistics.
    /// </summary>
    [Test]
    public async Task CrewRetentionTaskProgressAfterRoundEnd()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var stationMap = await pair.CreateTestMap();
        var offMap = await pair.CreateTestMap();

        var stationSystem = server.System<StationSystem>();
        var recordsSystem = server.System<StationRecordsSystem>();

        var mapProto = server.ProtoMan.Index<GameMapPrototype>(StationMapId);

        var station = EntityUid.Invalid;
        var crewMobOnStation = EntityUid.Invalid;
        var crewMobEvacuated = EntityUid.Invalid;
        var crewMobDeleted = EntityUid.Invalid;
        var performerMob = EntityUid.Invalid;
        var cardEntity = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            station = stationSystem.InitializeNewStation(mapProto.Stations["Station"], null, "Retention Test Station");
            stationSystem.AddMainGridToStation(station, stationMap.Grid.Owner);
            stationSystem.AddGridToStation(station, offMap.Grid.Owner);

            // Mirror the same three crew cases as CrewOnStationIsNotCountedAsEvacuated.
            crewMobOnStation = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);
            crewMobEvacuated = entMan.SpawnEntity("MobHuman", offMap.GridCoords);
            crewMobDeleted = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);

            var records = entMan.GetComponent<StationRecordsComponent>(station);
            recordsSystem.CreateGeneralRecord(station, null, "On Station Crewman", 30, "Human",
                Gender.Male, PassengerJobId, null, null, null, records, crewEntity: crewMobOnStation);
            recordsSystem.CreateGeneralRecord(station, null, "Evacuated Crewman", 25, "Human",
                Gender.Female, PassengerJobId, null, null, null, records, crewEntity: crewMobEvacuated);
            recordsSystem.CreateGeneralRecord(station, null, "Lost Crewman", 35, "Human",
                Gender.Male, PassengerJobId, null, null, null, records, crewEntity: crewMobDeleted);

            entMan.DeleteEntity(crewMobDeleted);

            // Performer: a mob on the station grid that carries RailroadableComponent.
            // TryGetStationStats falls back to GetOwningStation which resolves the station
            // via the grid's StationMemberComponent (set by AddMainGridToStation above).
            performerMob = entMan.SpawnEntity("MobHuman", stationMap.GridCoords);
            entMan.EnsureComponent<RailroadableComponent>(performerMob);

            // Card entity: carries both the task component and the performer reference.
            cardEntity = entMan.SpawnEntity(null, stationMap.GridCoords);
            entMan.EnsureComponent<RailroadCrewRetentionTaskComponent>(cardEntity);
            var performer = entMan.EnsureComponent<RailroadCardPerformerComponent>(cardEntity);
            performer.Performer = (performerMob, entMan.GetComponent<RailroadableComponent>(performerMob));
        });

        await server.WaitAssertion(() =>
        {
            // A single event must first update StationCrewStatisticsSystem (Crew=3, LostCrew=1,
            // EvacuatedCrew=1) and then update the retention task (after ordering ensures this).
            entMan.EventBus.RaiseEvent(EventSource.Local,
                new GameRunLevelChangedEvent(GameRunLevel.InRound, GameRunLevel.PostRound));

            var task = entMan.GetComponent<RailroadCrewRetentionTaskComponent>(cardEntity);
            // alive = Crew - LostCrew = 3 - 1 = 2; ratio = EvacuatedCrew / alive = 1 / 2 = 0.5
            // This assertion breaks if the after-ordering on StationCrewStatisticsSystem is removed.
            Assert.That(task.Progress, Is.EqualTo(0.5f).Within(0.001f),
                "Expected retention task Progress == 0.5 (1 evacuated / 2 alive crew).");
        });

        await pair.CleanReturnAsync();
    }
}
