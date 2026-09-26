using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Content.IntegrationTests;
using Content.IntegrationTests.Pair;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Robust.Shared;
using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Benchmarks._Starlight.Fluids;

/// <summary>
/// Measures the server-side cost of moving organic mobs through puddles and leaving footprints.
/// </summary>
[Virtual]
[GcServer(true)]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[InvocationCount(1, 1)]
public class PuddleFootprintBenchmark
{
    private const int MovementSteps = 32;

    private static readonly EntProtoId Mob = "MobHuman";
    private static readonly EntProtoId Footprint = "Footprint";
    private static readonly ProtoId<ContentTileDefinition> Tile = "Plating";

    private TestPair _pair = default!;
    private IEntityManager _entities = default!;
    private IPrototypeManager _prototypes = default!;
    private SharedMapSystem _map = default!;
    private SharedTransformSystem _transform = default!;
    private PuddleSystem _puddle = default!;
    private ITileDefinitionManager _tileDefinition = default!;

    private TestMapData _testMap = default!;
    private EntityUid[] _mobs = default!;

    /// <summary>
    /// Number of independently moving mobs in the benchmark workload.
    /// </summary>
    [Params(1, 32, 128)]
    public int MobCount { get; set; }

    /// <summary>
    /// Starts the integration-test server used by all benchmark iterations.
    /// </summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        ProgramShared.PathOffset = "../../../../";
        PoolManager.Startup();

        _pair = await PoolManager.GetServerClient(
            testContext: new ExternalTestContext("Benchmark", StreamWriter.Null));

        var server = _pair.Server;
        _entities = server.ResolveDependency<IEntityManager>();
        _prototypes = server.ResolveDependency<IPrototypeManager>();
        _map = server.System<SharedMapSystem>();
        _transform = server.System<SharedTransformSystem>();
        _puddle = server.System<PuddleSystem>();
        _tileDefinition = server.ResolveDependency<ITileDefinitionManager>();
    }

    /// <summary>
    /// Stops the integration-test server after all benchmark iterations finish.
    /// </summary>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _pair.DisposeAsync();
        PoolManager.Shutdown();
    }

    /// <summary>
    /// Builds a dry two-tile lane for every mob as a control for transform and movement-event costs.
    /// </summary>
    [IterationSetup(Target = nameof(DryTraversal))]
    public void SetupDryTraversal() => SetupMap(withPuddles: false);

    /// <summary>
    /// Builds a two-tile lane for every mob with a puddle on the first tile.
    /// </summary>
    [IterationSetup(Target = nameof(PuddleTraversal))]
    public void SetupPuddleTraversal() => SetupMap(withPuddles: true);

    /// <summary>
    /// Removes all entities and tiles created for the dry traversal iteration.
    /// </summary>
    [IterationCleanup(Target = nameof(DryTraversal))]
    public void CleanupDryTraversal() => CleanupMap();

    /// <summary>
    /// Removes all entities and tiles created for the puddle traversal iteration.
    /// </summary>
    [IterationCleanup(Target = nameof(PuddleTraversal))]
    public void CleanupPuddleTraversal()
    {
        ValidateFootprintsCreated();
        CleanupMap();
    }

    /// <summary>
    /// Moves every mob back and forth over dry tiles.
    /// </summary>
    [Benchmark(Baseline = true), BenchmarkCategory("Traversal")]
    public Task DryTraversal() => MoveMobs();

    /// <summary>
    /// Moves every mob between a puddle and a dry tile, repeatedly picking up liquid and leaving footprints.
    /// </summary>
    [Benchmark, BenchmarkCategory("Traversal")]
    public Task PuddleTraversal() => MoveMobs();

    private void SetupMap(bool withPuddles)
    {
        _testMap = _pair.CreateTestMap().GetAwaiter().GetResult();
        _mobs = new EntityUid[MobCount];

        _pair.Server.WaitPost(() =>
        {
            var tile = new Tile(_tileDefinition[Tile].TileId);

            for (var i = 0; i < MobCount; i++)
            {
                var puddleTile = new Vector2i(0, i);
                var footprintTile = new Vector2i(1, i);
                _map.SetTile(_testMap.Grid, _testMap.Grid, puddleTile, tile);
                _map.SetTile(_testMap.Grid, _testMap.Grid, footprintTile, tile);

                if (withPuddles)
                {
                    var tileRef = _map.GetTileRef(_testMap.Grid, _testMap.Grid, puddleTile);
                    if (!_puddle.TrySpillAt(tileRef, new Solution("Water", FixedPoint2.New(20)), out _, sound: false))
                        throw new System.InvalidOperationException("Failed to create a benchmark puddle.");
                }

                var coordinates = new EntityCoordinates(_testMap.Grid, 0.25f, i + 0.5f);
                _mobs[i] = _pair.Server.EntMan.SpawnAttachedTo(Mob, coordinates);
            }
        }).GetAwaiter().GetResult();
    }

    private Task MoveMobs() => _pair.Server.WaitPost(() =>
                                    {
                                        for (var step = 0; step < MovementSteps; step++)
                                        {
                                            var x = step % 2 == 0 ? 0.75f : 1.25f;

                                            for (var i = 0; i < _mobs.Length; i++)
                                                _transform.SetCoordinates(_mobs[i], new EntityCoordinates(_testMap.Grid, x, i + 0.5f));
                                        }
                                    });

    private void CleanupMap()
    {
        _pair.Server.WaitPost(() => _map.QueueDeleteMap(_testMap.MapId)).GetAwaiter().GetResult();
        _pair.Server.WaitRunTicks(2).GetAwaiter().GetResult();
    }

    private void ValidateFootprintsCreated()
    {
        if (!_prototypes.HasIndex<EntityPrototype>(Footprint))
            return;

        var query = _entities.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == Footprint.Id)
                return;
        }

        throw new System.InvalidOperationException("Puddle traversal did not create any footprints.");
    }
}
