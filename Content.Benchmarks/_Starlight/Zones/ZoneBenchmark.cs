using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Content.IntegrationTests;
using Content.IntegrationTests.Pair;
using Content.Server._Starlight.Zones;
using Content.Shared._Starlight.Zones;
using Robust.Shared;
using Robust.Shared.Analyzers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Benchmarks._Starlight.Zones;

[Virtual]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ZoneBenchmark
{
    public const string Map = "Maps/_Starlight/Stations/Box.yml";

    private const int ZoneSize = 24;

    private static readonly ProtoId<ZonePrototype>[] _paintedZones =
    [
        "Hallway", "Maintenance", "Cargo", "Medical", "Engineering", "Security",
    ];

    private TestPair _pair = default!;
    private IEntityManager _entMan = default!;
    private ZoneSystem _zones = default!;
    private SharedMapSystem _maps = default!;

    private Entity<ZoneGridComponent> _grid;
    private MapGridComponent _gridComp = default!;

    private const int Lookups = 1000;

    private Vector2i[] _scattered = default!;

    private Vector2i _hotTile;

    private Vector2i _wallTile;

    private EntityUid _wall;

    [GlobalSetup]
    public void Setup()
    {
        ProgramShared.PathOffset = "../../../../";
        PoolManager.Startup();

        _pair = PoolManager.GetServerClient(testContext: new ExternalTestContext("Benchmark", StreamWriter.Null))
            .GetAwaiter().GetResult();

        _entMan = _pair.Server.ResolveDependency<IEntityManager>();

        _pair.Server.WaitPost(() =>
        {
            var opts = DeserializationOptions.Default with { InitializeMaps = true };

            if (!_entMan.System<MapLoaderSystem>().TryLoadMap(new ResPath(Map), out _, out var grids, opts))
                throw new Exception("Map load failed");

            _zones = _entMan.System<ZoneSystem>();
            _maps = _entMan.System<SharedMapSystem>();

            var biggest = FindBiggestGrid(grids);
            _gridComp = _entMan.GetComponent<MapGridComponent>(biggest);
            _grid = (biggest, _entMan.EnsureComponent<ZoneGridComponent>(biggest));

            PaintZones();
            _zones.FullRebuild(_grid);
            CollectTiles();
        }).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _pair.DisposeAsync();
        PoolManager.Shutdown();
    }

    private EntityUid FindBiggestGrid(IReadOnlySet<Entity<MapGridComponent>> grids)
    {
        var best = EntityUid.Invalid;
        var bestTiles = -1;

        foreach (var grid in grids)
        {
            var count = 0;
            var enumerator = _maps.GetAllTiles(grid.Owner, grid.Comp);

            while (enumerator.MoveNext(out _))
            {
                count++;
            }

            if (count <= bestTiles)
                continue;

            best = grid.Owner;
            bestTiles = count;
        }

        return best == EntityUid.Invalid ? throw new Exception("Map had no grids") : best;
    }

    private void PaintZones()
    {
        var bounds = GetTileBounds();
        var index = 0;

        for (var x = bounds.Left; x < bounds.Right; x += ZoneSize)
        for (var y = bounds.Bottom; y < bounds.Top; y += ZoneSize)
        {
            var rect = new Box2i(x, y, x + ZoneSize, y + ZoneSize);
            _zones.PaintZoneRect(_grid, rect, _paintedZones[index++ % _paintedZones.Length]);
        }
    }

    private Box2i GetTileBounds()
    {
        var min = new Vector2i(int.MaxValue, int.MaxValue);
        var max = new Vector2i(int.MinValue, int.MinValue);

        var enumerator = _maps.GetAllTiles(_grid.Owner, _gridComp);

        while (enumerator.MoveNext(out var tile))
        {
            min = Vector2i.ComponentMin(min, tile.Value.GridIndices);
            max = Vector2i.ComponentMax(max, tile.Value.GridIndices);
        }

        return new Box2i(min.X, min.Y, max.X + 1, max.Y + 1);
    }

    private void CollectTiles()
    {
        var tiles = new List<Vector2i>();
        var enumerator = _maps.GetAllTiles(_grid.Owner, _gridComp);

        while (enumerator.MoveNext(out var tile))
            tiles.Add(tile.Value.GridIndices);

        if (tiles.Count == 0)
            throw new Exception("Grid had no tiles");

        _scattered = new Vector2i[Lookups];

        for (var i = 0; i < Lookups; i++)
            _scattered[i] = tiles[i * 37 % tiles.Count];
        _hotTile = tiles[tiles.Count / 2];

        _wallTile = _hotTile;

        foreach (var tile in tiles)
        {
            if (_zones.GetRegion(_grid.Owner, tile) == SharedZoneSystem.NoRegion)
                continue;

            _wallTile = tile;
            break;
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Lookups), BenchmarkCategory("Lookup")]
    public int LookupSameTile()
    {
        var total = 0;

        for (var i = 0; i < Lookups; i++)
            total += SharedZoneSystem.GetZoneId(_grid.Comp, _hotTile);

        return total;
    }

    [Benchmark(OperationsPerInvoke = Lookups), BenchmarkCategory("Lookup")]
    public int LookupScattered()
    {
        var total = 0;

        foreach (var tile in _scattered)
            total += SharedZoneSystem.GetZoneId(_grid.Comp, tile);

        return total;
    }

    [Benchmark, BenchmarkCategory("Upkeep")]
    public void IdleTick() => _zones.Update(1f / 60f);

    [Benchmark, BenchmarkCategory("Upkeep")]
    public void FullRebuild() => _zones.FullRebuild(_grid);

    [Benchmark, BenchmarkCategory("Building")]
    public void ProcessBuiltWall() => _zones.Update(1f / 60f);

    [IterationSetup(Target = nameof(ProcessBuiltWall))]
    public void SpawnWall() =>
        _pair.Server.WaitPost(() => _wall = _entMan.SpawnEntity("WallSolid", _maps.GridTileToLocal(_grid.Owner, _gridComp, _wallTile))).GetAwaiter().GetResult();

    [IterationCleanup(Target = nameof(ProcessBuiltWall))]
    public void RemoveWall() =>
        _pair.Server.WaitPost(() =>
        {
            _entMan.DeleteEntity(_wall);
            _zones.Update(1f / 60f);
        }).GetAwaiter().GetResult();
}
