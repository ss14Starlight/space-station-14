#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._Starlight.Station;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Station;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight;

/// <summary>
/// Round-level integration tests for StationCrewStatisticsSystem.
/// These tests use a real game round (DummyTicker = false) to verify
/// the production spawn path — i.e. that PlayerSpawnCompleteEvent correctly
/// sets GeneralStationRecord.Entity to the player's MOB, not to the ID card.
/// </summary>
[TestFixture]
[TestOf(typeof(StationCrewStatisticsSystem))]
public sealed class StationCrewStatisticsRoundTest
{
    // Assistant (not Passenger): Passenger is hidden:true which causes EnsureValid to strip
    // it from the character profile's _jobPreferences, making job assignment impossible.
    // Assistant is the visible, setPreference:true counterpart that uses the same gear.
    private static readonly ProtoId<JobPrototype> AssistantJob = "Assistant";

    private static readonly string RoundTestMapId = "CrewStatsRoundTestMap";

    private const int WaitTicks = 10;

    [TestPrototypes]
    private static readonly string RoundTestPrototypes = $@"
- type: gameMap
  id: {RoundTestMapId}
  minPlayers: 0
  mapName: {RoundTestMapId}
  mapPath: /Maps/Test/empty.yml
  stations:
    Empty:
      mapNameTemplate: {RoundTestMapId}
      stationProto: StandardNanotrasenStationTestOnly
      components:
      - type: StationCrewStatistics
      - type: StationJobs
        availableJobs:
          {AssistantJob}: [ -1, -1 ]
";

    /// <summary>
    /// Real spawn path regression guard.
    /// Verifies that after a real round start the station's GeneralStationRecord.Entity
    /// points at the player's MOB entity, not at the ID card or PDA in the "id" inventory slot.
    /// Also verifies that ending the round with the player standing on-station yields
    /// Crew==1, LostCrew==0, EvacuatedCrew==0 via StationCrewStatisticsComponent.
    ///
    /// Bug shape guarded: before the fix, CreateGeneralRecord set Entity from the id-card
    /// entity (via the `idUid` path) rather than from the mob, so the MapID comparison in
    /// CheckStation always failed and every player was counted as evacuated or lost.
    /// This test breaks if the `crewEntity: player` argument is removed from the
    /// PlayerSpawnCompleteEvent handler in StationRecordsSystem.
    /// </summary>
    [Test]
    public async Task RealSpawnSetsRecordEntityToPlayerMob()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            InLobby = true,
            Connected = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var sharedRecords = server.System<SharedStationRecordsSystem>();

        // Point the server at our test map.
        server.CfgMan.SetCVar(CCVars.GameMap, RoundTestMapId);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        Assert.That(pair.Client.AttachedEntity, Is.Null);

        // Configure the player's character profile so AssignJobs can place them.
        // Steps:
        //   1. SetJobPreferences: stores AssistantJob in the profile's _jobPreferences.
        //      Assistant (unlike Passenger) has hidden:false so EnsureValid keeps it.
        //   2. SetJobPriorities: stores the priority in prefs.JobPriorities (Starlight-specific store).
        //   3. Enable the profile: SetJobPreferences does NOT call AsEnabled(), so the default
        //      character profile has Enabled=false. GetPlayersJobCandidates skips disabled profiles,
        //      so the player would get no jobs and stay ReadyToPlay. Explicitly enable it here,
        //      mirroring the AddDummyPlayers helper pattern.
        await pair.SetJobPreferences([AssistantJob]);
        await pair.SetJobPriorities((AssistantJob, JobPriority.Medium));
        await server.WaitPost(() =>
        {
            var prefMan = server.ResolveDependency<IServerPreferencesManager>();
            var userId = pair.Client.User!.Value;
            var prefs = prefMan.GetPreferences(userId);
            var profile = (HumanoidCharacterProfile)prefs.Characters[0];
            prefMan.SetProfile(userId, 0, profile.AsEnabled()).Wait();
        });

        // Ready the single player and start the round.
        ticker.ToggleReadyAll(true);
        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(WaitTicks);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound),
            "Round did not start — check that the map prototype is valid.");
        Assert.That(ticker.PlayerGameStatuses[pair.Client.User!.Value], Is.EqualTo(PlayerGameStatus.JoinedGame),
            "Player should have joined the game after round start.");
        Assert.That(pair.Player?.AttachedEntity, Is.Not.Null,
            "Player should have an attached entity after joining the round.");

        // ── Assertion A: record.Entity == player mob ──────────────────────────────
        await server.WaitAssertion(() =>
        {
            // The server-side mob entity attached to the connected player session.
            var playerMob = pair.Player!.AttachedEntity!.Value;
            Assert.That(entMan.EntityExists(playerMob), "Player mob must exist after round start.");

            // Find the station that has StationCrewStatisticsComponent.
            var station = EntityUid.Invalid;
            var statsQuery = entMan.EntityQueryEnumerator<StationCrewStatisticsComponent>();
            while (statsQuery.MoveNext(out var uid, out _))
            {
                station = uid;
                break;
            }
            Assert.That(station, Is.Not.EqualTo(EntityUid.Invalid),
                "No entity with StationCrewStatisticsComponent found.");

            // Exactly one GeneralStationRecord should exist (one player spawned).
            var records = sharedRecords.GetRecordsOfType<GeneralStationRecord>(station).ToList();
            Assert.That(records, Has.Count.EqualTo(1),
                "Expected exactly one GeneralStationRecord after a single player spawned.");

            var record = records[0].Item2;
            // This assertion is the production-path regression guard:
            // it breaks if the `crewEntity: player` argument is dropped from
            // StationRecordsSystem.OnPlayerSpawn → CreateGeneralRecord.
            Assert.That(record.Entity, Is.EqualTo(entMan.GetNetEntity(playerMob)),
                "GeneralStationRecord.Entity must be the player's MOB, not the ID card entity.");
        });

        // ── Assertion B: on-station player counts correctly at round end ──────────
        await server.WaitPost(() => ticker.EndRound());
        await pair.RunTicksSync(WaitTicks);

        await server.WaitAssertion(() =>
        {
            var station = EntityUid.Invalid;
            var statsQuery = entMan.EntityQueryEnumerator<StationCrewStatisticsComponent>();
            while (statsQuery.MoveNext(out var uid, out _))
            {
                station = uid;
                break;
            }
            Assert.That(station, Is.Not.EqualTo(EntityUid.Invalid),
                "No entity with StationCrewStatisticsComponent found after round end.");

            var stats = entMan.GetComponent<StationCrewStatisticsComponent>(station);
            Assert.Multiple(() =>
            {
                // Player was standing on the station — must be counted as present, not evacuated or lost.
                Assert.That(stats.Crew, Is.EqualTo(1), "Expected Crew == 1 after round end.");
                Assert.That(stats.LostCrew, Is.EqualTo(0), "Expected LostCrew == 0.");
                Assert.That(stats.EvacuatedCrew, Is.EqualTo(0), "Expected EvacuatedCrew == 0.");
            });
        });

        // Clean up: return to lobby and release the pair.
        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(WaitTicks);
        await pair.ReallyBeIdle();
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Full-lifecycle multi-crew evacuation test.
    ///
    /// Scenario (4 crew):
    ///   - 2 players board the emergency shuttle and ride it to CentComm   → EvacuatedCrew == 2
    ///   - 1 player stays on-station                                        → counted in Crew but not evacuated/lost
    ///   - 1 player's mob is deleted before round end                       → LostCrew == 1
    ///
    /// Fallback (if deleting a player mob causes error-level log spam):
    ///   Skip the delete; expect 2 evacuated, 2 stayed, LostCrew == 0.
    ///
    /// Regression guard: if the MainGrids comparison were reverted to
    ///   comparing against the nullspace station entity's MapID
    ///   (which is MapId.Nullspace = 0) instead of the real station map,
    ///   then *every* mob would be on a different map than MapId.Nullspace,
    ///   so EvacuatedCrew would be 4 (or 3) instead of 2.
    ///   The assertion "EvacuatedCrew == 2" would catch that regression.
    /// </summary>
    [Test]
    public async Task MultiCrewEvacuationSplitsStatistics()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            InLobby = true,
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var sharedRecords = server.System<SharedStationRecordsSystem>();

        // ── Save and override CVars ──────────────────────────────────────────────
        var prevGameMap = server.CfgMan.GetCVar(CCVars.GameMap);
        var prevShuttleEnabled = server.CfgMan.GetCVar(CCVars.EmergencyShuttleEnabled);
        var prevDockTime = server.CfgMan.GetCVar(CCVars.EmergencyShuttleDockTime);
        var prevDummyTicker = server.CfgMan.GetCVar(CCVars.GameDummyTicker);

        server.CfgMan.SetCVar(CCVars.GameMap, "StarlightCog");
        server.CfgMan.SetCVar(CCVars.EmergencyShuttleEnabled, true);
        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);

        try
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

            // ── Configure the real client player ────────────────────────────────
            await pair.SetJobPreferences([AssistantJob]);
            await pair.SetJobPriorities((AssistantJob, JobPriority.Medium));
            await server.WaitPost(() =>
            {
                var prefMan = server.ResolveDependency<IServerPreferencesManager>();
                var userId = pair.Client.User!.Value;
                var prefs = prefMan.GetPreferences(userId);
                var profile = (HumanoidCharacterProfile)prefs.Characters[0];
                prefMan.SetProfile(userId, 0, profile.AsEnabled()).Wait();
            });

            // ── Add 3 dummy players (Assistant job, each enabled) ────────────────
            var jobPriorities = new Dictionary<ProtoId<JobPrototype>, JobPriority>
            {
                [AssistantJob] = JobPriority.Medium,
            };
            var dummies = (await pair.AddDummyPlayers(jobPriorities, [AssistantJob], 3)).ToList();

            // ── Ready all and start the round ────────────────────────────────────
            ticker.ToggleReadyAll(true);
            await server.WaitPost(() => ticker.StartRound());
            await pair.RunTicksSync(WaitTicks * 3);

            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound),
                "Round did not start — verify StarlightCog map prototype and EmergencyShuttleEnabled.");
            Assert.That(pair.Player?.AttachedEntity, Is.Not.Null,
                "Real client should have an attached mob after round start.");
            Assert.That(dummies.All(d => d.AttachedEntity != null),
                "All dummy sessions should have attached mobs after round start.");

            // ── Collect all 4 mobs ──────────────────────────────────────────────
            var realMob = pair.Player!.AttachedEntity!.Value;
            var dummyMobs = dummies.Select(d => d.AttachedEntity!.Value).ToList();
            // mob0/mob1 → board shuttle; mob2 → stays on station; real client mob → will be deleted (LostCrew)
            var shuttleBoarders = new[] { dummyMobs[0], dummyMobs[1] };
            // dummyMobs[2] stays on station — no action needed, stats assertion covers it implicitly.
            var toDeleteMob = realMob;

            // ── Verify 4 GeneralStationRecords ───────────────────────────────────
            await server.WaitAssertion(() =>
            {
                var station = EntityUid.Invalid;
                var statsQuery = entMan.EntityQueryEnumerator<StationCrewStatisticsComponent>();
                while (statsQuery.MoveNext(out var uid, out _))
                {
                    station = uid;
                    break;
                }
                Assert.That(station, Is.Not.EqualTo(EntityUid.Invalid), "Station with StationCrewStatisticsComponent not found.");

                var records = sharedRecords.GetRecordsOfType<GeneralStationRecord>(station).ToList();
                Assert.That(records, Has.Count.EqualTo(4),
                    $"Expected 4 GeneralStationRecords after 4 players spawned; got {records.Count}.");

                var allMobs = new[] { realMob, dummyMobs[0], dummyMobs[1], dummyMobs[2] };
                var recordedEntities = records.Select(r => r.Item2.Entity).ToHashSet();
                foreach (var mob in allMobs)
                {
                    var netMob = entMan.GetNetEntity(mob);
                    Assert.That(recordedEntities.Contains(netMob),
                        $"No GeneralStationRecord found for mob {mob} (net: {netMob}).");
                }
            });

            // ── Set up shuttle timing (mirrors EvacShuttleTest) ─────────────────
            var shuttleSys = server.System<ShuttleSystem>();
            var evacSys = server.System<EmergencyShuttleSystem>();
            evacSys.TransitTime = shuttleSys.DefaultTravelTime;

            server.CfgMan.SetCVar(CCVars.EmergencyShuttleDockTime, 2);

            // Locate the station, shuttle, and centcomm map.
            var stationEnt = (Entity<StationCentcommComponent>)
                entMan.AllComponentsList<StationCentcommComponent>().Single();
            var shuttleData = entMan.GetComponent<StationEmergencyShuttleComponent>(stationEnt);
            var shuttle = shuttleData.EmergencyShuttle!.Value;
            var centcommMap = stationEnt.Comp.MapEntity!.Value;

            var stationDataComp = entMan.GetComponent<StationDataComponent>(stationEnt);
            // Identify the station map from MainGrids (or fall back to Grids).
            var stationGrids = stationDataComp.MainGrids.Count > 0
                ? stationDataComp.MainGrids
                : stationDataComp.Grids;
            var stationGridUid = stationGrids.First();
            var stationMap = server.Transform(stationGridUid).MapUid!.Value;

            Assert.That(stationMap, Is.Not.EqualTo(centcommMap),
                "Station and CentComm should be on different maps before evacuation.");

            // ── Call the shuttle and wait for it to dock ─────────────────────────
            await pair.WaitCommand("callshuttle 0:02");
            await pair.RunSeconds(3);

            var shuttleXform = server.Transform(shuttle);
            Assert.That(shuttleXform.MapUid, Is.EqualTo(stationMap),
                "Shuttle should have docked at the station map after callshuttle.");

            // ── Teleport 2 mobs onto the shuttle grid ────────────────────────────
            await server.WaitPost(() =>
            {
                var xformSys = entMan.System<SharedTransformSystem>();
                foreach (var mob in shuttleBoarders)
                {
                    xformSys.SetCoordinates(mob, new EntityCoordinates(shuttle, Vector2.Zero));
                }
            });

            // Verify the boarders are on the shuttle.
            await server.WaitAssertion(() =>
            {
                foreach (var mob in shuttleBoarders)
                {
                    Assert.That(server.Transform(mob).GridUid, Is.EqualTo(shuttle),
                        $"Mob {mob} should be on the shuttle grid after teleport.");
                }
            });

            // ── Delete the 4th mob to produce LostCrew == 1 ─────────────────────
            // Note: MindSystem.OnMindContainerTerminating uses Log.Warning (not Log.Error)
            // when no ghost spawn location is found, so deletion is test-safe.
            await server.WaitPost(() =>
            {
                entMan.DeleteEntity(toDeleteMob);
            });
            await pair.RunTicksSync(5);

            // ── Wait for the shuttle to leave the station ────────────────────────
            await pair.RunSeconds(2);

            Assert.That(entMan.Count<FTLMapComponent>(), Is.EqualTo(1),
                "Expected the shuttle to be in FTL transit after docking time elapsed.");

            // ── Shuttle arrives at CentComm ──────────────────────────────────────
            await pair.RunSeconds(shuttleSys.DefaultTravelTime);
            Assert.That(shuttleXform.MapUid, Is.EqualTo(centcommMap),
                "Shuttle should have arrived at CentComm after transit.");

            // ── Round ends ───────────────────────────────────────────────────────
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound),
                "Round should be in PostRound after shuttle arrives at CentComm.");

            // ── Assert shuttle boarders are genuinely on a different map ─────────
            await server.WaitAssertion(() =>
            {
                foreach (var mob in shuttleBoarders)
                {
                    Assert.That(server.Transform(mob).MapUid, Is.EqualTo(centcommMap),
                        $"Shuttle boarder {mob} should be on CentComm map at PostRound.");
                    Assert.That(server.Transform(mob).MapUid, Is.Not.EqualTo(stationMap),
                        $"Shuttle boarder {mob} must NOT be on the station map (proves they're on CentComm).");
                }
            });

            // ── Assert StationCrewStatisticsComponent ────────────────────────────
            await server.WaitAssertion(() =>
            {
                var station = EntityUid.Invalid;
                var statsQuery = entMan.EntityQueryEnumerator<StationCrewStatisticsComponent>();
                while (statsQuery.MoveNext(out var uid, out _))
                {
                    station = uid;
                    break;
                }
                Assert.That(station, Is.Not.EqualTo(EntityUid.Invalid),
                    "Station with StationCrewStatisticsComponent not found at PostRound.");

                var stats = entMan.GetComponent<StationCrewStatisticsComponent>(station);
                Assert.Multiple(() =>
                {
                    Assert.That(stats.Crew, Is.EqualTo(4), $"Expected Crew == 4; got {stats.Crew}.");
                    Assert.That(stats.EvacuatedCrew, Is.EqualTo(2), $"Expected EvacuatedCrew == 2; got {stats.EvacuatedCrew}.");
                    Assert.That(stats.LostCrew, Is.EqualTo(1), $"Expected LostCrew == 1; got {stats.LostCrew}.");
                });
            });
        }
        finally
        {
            // ── Restore all CVars even if an assertion failed ────────────────────
            server.CfgMan.SetCVar(CCVars.EmergencyShuttleDockTime, prevDockTime);
            server.CfgMan.SetCVar(CCVars.EmergencyShuttleEnabled, prevShuttleEnabled);
            server.CfgMan.SetCVar(CCVars.GameDummyTicker, prevDummyTicker);
            server.CfgMan.SetCVar(CCVars.GameMap, prevGameMap);
        }

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(WaitTicks);
        await pair.ReallyBeIdle();
        await pair.CleanReturnAsync();
    }
}
