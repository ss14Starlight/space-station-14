using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Starlight.CryoTeleportation;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.CryoTeleportation;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.CCVar;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.CryoTeleportation;

[TestFixture]
[TestOf(typeof(CryoTeleportationSystem))]
public sealed class CryoTeleportationTest : GameTest
{
    private const string Map = "CryoTeleportationTestMap";
    private static readonly EntProtoId _cryoPod = "CryogenicSleepUnit";
    private static readonly ProtoId<JobPrototype> _passenger = "Assistant";
    private static readonly ProtoId<JobPrototype> _mime = "Mime";

    // Long enough to cover the auto-cryo refresh cooldown (5s) plus a margin.
    private const float WaitForCryo = 8f;

    [TestPrototypes]
    private static readonly string _prototypes = $@"
- type: gameMap
  id: {Map}
  mapName: {Map}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Empty""
        - type: StationJobs
          availableJobs:
            {_passenger}: [ -1, -1 ]
            {_mime}: [ 1, 1 ]
            K9: [ 1, 1 ]
";

    // Dirty: tests die, revive, and disconnect the test client.
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [SidedDependency(Side.Server)] private readonly GameTicker _ticker = default!;
    [SidedDependency(Side.Server)] private readonly GhostSystem _ghost = default!;
    [SidedDependency(Side.Server)] private readonly SharedMindSystem _mind = default!;
    [SidedDependency(Side.Server)] private readonly MobStateSystem _mobState = default!;
    [SidedDependency(Side.Server)] private readonly StationSystem _station = default!;
    [SidedDependency(Side.Server)] private readonly StationJobsSystem _stationJobs = default!;
    [SidedDependency(Side.Server)] private readonly IServerPreferencesManager _prefs = default!;

    /// <summary>
    /// /ghost while alive leaves a mindless body. Auto-cryo must still reopen its slot.
    /// </summary>
    [TestCase("Mime")]
    [TestCase("K9")]
    public async Task GhostWhileAliveReturnsSlot(string jobId)
    {
        ProtoId<JobPrototype> job = jobId;
        var (body, station, pod) = await StartRoundAs(job);
        var user = ServerSession!.UserId;

        await Server.WaitAssertion(() =>
        {
            AssertSlots(station, job, 0);
            Assert.That(_mind.TryGetMind(body, out var mindId, out _));
            Assert.That(_ghost.OnGhostAttempt(mindId, true, viaCommand: true));
        });

        await RunSeconds(WaitForCryo);

        await Server.WaitAssertion(() =>
        {
            AssertStored(pod, body);
            AssertSlots(station, job, 1);
            AssertNotHeld(station, user, job);
        });

        await Server.WaitPost(() => _ticker.RestartRound());
    }

    /// <summary>
    /// Die, ghost, new life elsewhere, old body revived. The revived body gets cryo'd and only its slot reopens.
    /// </summary>
    [Test]
    public async Task RevivedBodyAfterNewLifeReturnsOnlyOldSlot()
    {
        var (body, station, pod) = await StartRoundAs(_mime);
        var session = ServerSession!;
        EntityUid newBody = default;

        await Server.WaitAssertion(() =>
        {
            Assert.That(_mind.TryGetMind(body, out var mindId, out _));
            _mobState.ChangeMobState(body, MobState.Dead);
            Assert.That(_ghost.OnGhostAttempt(mindId, true, viaCommand: true));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var profile = _prefs.GetPreferences(session.UserId).Characters[0];
            _ticker.MakeJoinGame(session, profile, station, _passenger);
        });
        await RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            newBody = session.AttachedEntity!.Value;
            Assert.That(newBody, Is.Not.EqualTo(body));
            _mobState.ChangeMobState(body, MobState.Alive);
        });

        await RunSeconds(WaitForCryo);

        await Server.WaitAssertion(() =>
        {
            AssertStored(pod, body);
            AssertSlots(station, _mime, 1);
            AssertNotHeld(station, session.UserId, _mime);

            Assert.That(_stationJobs.TryGetPlayerJobs(station, session.UserId, out var jobs));
            Assert.That(jobs, Does.Contain(_passenger));
            Assert.That(session.AttachedEntity, Is.EqualTo(newBody));
            Assert.That(SEntMan.HasComponent<CryostorageContainedComponent>(newBody), Is.False);
        });

        await Server.WaitPost(() => _ticker.RestartRound());
    }

    /// <summary>
    /// Disconnecting in the body goes through cryostorage's own slot return. Guards that path.
    /// </summary>
    [Test]
    public async Task DisconnectInBodyReturnsSlot()
    {
        var (body, station, pod) = await StartRoundAs(_mime);
        var user = ServerSession!.UserId;

        var clientNet = Client.ResolveDependency<IClientNetManager>();
        await Client.WaitPost(() => clientNet.ClientDisconnect("Testing auto-cryo"));
        await RunTicksSync(20);

        await RunSeconds(WaitForCryo);

        await Server.WaitAssertion(() =>
        {
            AssertStored(pod, body);
            AssertSlots(station, _mime, 1);
            AssertNotHeld(station, user, _mime);
        });

        await Server.WaitPost(() => _ticker.RestartRound());
    }

    /// <summary>
    /// A player who returns to their revived body before the timer runs out keeps it.
    /// </summary>
    [Test]
    public async Task ReturningToRevivedBodyCancelsCryo()
    {
        var (body, station, _) = await StartRoundAs(_mime);
        var session = ServerSession!;
        EntityUid mindId = default;

        await Server.WaitAssertion(() =>
        {
            Assert.That(_mind.TryGetMind(body, out mindId, out _));
            _mobState.ChangeMobState(body, MobState.Dead);
            Assert.That(_ghost.OnGhostAttempt(mindId, true, viaCommand: true));
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            _mobState.ChangeMobState(body, MobState.Alive);
            _mind.UnVisit(mindId);
        });

        await RunSeconds(WaitForCryo);

        await Server.WaitAssertion(() =>
        {
            Assert.That(session.AttachedEntity, Is.EqualTo(body));
            Assert.That(SEntMan.HasComponent<CryostorageContainedComponent>(body), Is.False);
            AssertSlots(station, _mime, 0);
        });

        await Server.WaitPost(() => _ticker.RestartRound());
    }

    /// <summary>
    /// Starts a round with the test player in <paramref name="job"/>, a zero-delay auto-cryo, and an empty pod.
    /// </summary>
    private async Task<(EntityUid Body, EntityUid Station, EntityUid Pod)> StartRoundAs(ProtoId<JobPrototype> job)
    {
        Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        Assert.That(_ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        await Pair.SetJobPreferences([job]);
        await Pair.SetJobPriorities((job, JobPriority.High));
        _ticker.ToggleReadyAll(true);
        await Server.WaitPost(() => _ticker.StartRound());
        await RunTicksSync(10);

        Pair.AssertJob(job);

        EntityUid body = default, station = default, pod = default;
        await Server.WaitAssertion(() =>
        {
            body = ServerSession!.AttachedEntity!.Value;

            var target = SComp<TargetCryoTeleportationComponent>(body);
            Assert.That(target.Station, Is.Not.Null);
            Assert.That(target.Job, Is.EqualTo(job));
            station = target.Station!.Value;

            SComp<StationCryoTeleportationComponent>(station).TransferDelay = TimeSpan.Zero;

            var grid = _station.GetLargestGrid(station);
            Assert.That(grid, Is.Not.Null);
            pod = SSpawnAtPosition(_cryoPod, new EntityCoordinates(grid!.Value, -0.5f, -0.5f));

            var cryo = SComp<CryostorageComponent>(pod);
            cryo.GracePeriod = TimeSpan.Zero;
            cryo.NoMindGracePeriod = TimeSpan.Zero;
        });

        return (body, station, pod);
    }

    private void AssertSlots(EntityUid station, ProtoId<JobPrototype> job, int expected)
    {
        Assert.That(_stationJobs.TryGetJobSlot(station, job, out var slots));
        Assert.That(slots, Is.EqualTo(expected), $"Open {job} slots");
    }

    private void AssertNotHeld(EntityUid station, NetUserId user, ProtoId<JobPrototype> job)
    {
        if (_stationJobs.TryGetPlayerJobs(station, user, out var jobs))
            Assert.That(jobs, Does.Not.Contain(job));
    }

    private void AssertStored(EntityUid pod, EntityUid body)
        => Assert.That(SComp<CryostorageComponent>(pod).StoredPlayers, Does.Contain(body));
}
