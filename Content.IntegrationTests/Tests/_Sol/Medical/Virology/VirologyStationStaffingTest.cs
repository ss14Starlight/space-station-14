using Content.Server.Maps;
using Content.Server.Station.Systems;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Maps;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
public sealed class VirologyStationStaffingTest
{
    // Strings (not ProtoId) so YAMLLinter static-field checks don't require map kinds on the client.
    private const string CorkMap = "SolCork";
    private const string SalternMap = "SolSaltern";
    private const string VirologyPool = "VirologyMapPool";
    private static readonly ProtoId<JobPrototype> VirologistJob = "Virologist";

    [Test]
    public async Task CorkAndSalternHaveConfiguredVirologistSlots()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var stationSystem = server.System<StationSystem>();
        var stationJobs = server.System<StationJobsSystem>();

        await server.WaitAssertion(() =>
        {
            var viroName = factory.GetComponentName<VirologyStationComponent>();

            AssertStation(proto, stationSystem, stationJobs, CorkMap, viroName, expectedSlots: 1);
            AssertStation(proto, stationSystem, stationJobs, SalternMap, viroName, expectedSlots: 3);

            Assert.That(proto.TryIndex<GameMapPoolPrototype>(VirologyPool, out var pool), Is.True);
            Assert.That(pool!.Maps, Does.Contain(CorkMap));
            Assert.That(pool.Maps, Does.Contain(SalternMap));
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertStation(
        IPrototypeManager proto,
        StationSystem stationSystem,
        StationJobsSystem stationJobs,
        string mapId,
        string viroName,
        int expectedSlots)
    {
        Assert.That(proto.TryIndex<GameMapPrototype>(mapId, out var map), Is.True, mapId);
        Assert.That(map!.Stations, Is.Not.Empty, mapId);

        var found = false;
        foreach (var (stationId, stationConfig) in map.Stations)
        {
            Assert.That(stationConfig.StationComponentOverrides.TryGetComponent(viroName, out _),
                Is.True,
                $"{mapId}/{stationId} missing VirologyStation");

            var station = stationSystem.InitializeNewStation(stationConfig, null, $"{mapId}-{stationId}");
            var jobs = stationJobs.GetRoundStartJobs(station);
            Assert.That(jobs.TryGetValue(VirologistJob, out var slots), Is.True, $"{mapId}/{stationId} missing Virologist");
            Assert.That(slots, Is.EqualTo(expectedSlots), $"{mapId} round-start slots");
            found = true;
        }

        Assert.That(found, Is.True, mapId);
    }
}
