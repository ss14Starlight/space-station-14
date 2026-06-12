#nullable enable
using System.Linq;
using Content.Server._Starlight.Station;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Station;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Shared.GameObjects;
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
    /// TEST A — real spawn path regression guard.
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
}
