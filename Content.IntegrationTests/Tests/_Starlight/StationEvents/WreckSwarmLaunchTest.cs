using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests._Starlight;
using Content.Server.Atmos.Components;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Server._Starlight.Salvage.Ruins;
using Content.Server._Starlight.StationEvents.Events;
using Content.Shared.Friction;
using Content.Shared.GameTicking.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Starlight.StationEvents;

[TestFixture]
[TestOf(typeof(WreckSwarmSystem))]
public sealed class WreckSwarmLaunchTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    #region Prototypes

    private const string TestStationProto = "TestWreckSwarmStation";
    private const string TestLaunchRuleProto = "TestWreckSwarmLaunch";
    private const string TestPlacementRuleProto = "TestWreckSwarmPlacement";
    private const string TestGeneratedRuleProto = "TestWreckSwarmGenerated";
    private const int StationSize = 12;
    private const int RandomSeed = 534;
    private const int CourtyardOuter = 40;
    private const int HoleInner = 12;
    private const int HoleOuter = 28;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestWreckSwarmStation
  parent: BaseStation
  components:
  - type: Transform
  - type: StationEventEligible

- type: entity
  parent: BaseGameRule
  id: TestWreckSwarmLaunch
  components:
  - type: GameRule
  - type: StationEvent
    duration: 60
    earliestStart: 0
    minimumPlayers: 0
    weight: 0
    globalAnnouncement: false
  - type: WreckSwarm
    fixedGrid: /Maps/Test/floor3x3.yml
    velocity: 50

- type: entity
  parent: BaseGameRule
  id: TestWreckSwarmPlacement
  components:
  - type: GameRule
  - type: StationEvent
    duration: 60
    earliestStart: 0
    minimumPlayers: 0
    weight: 0
    globalAnnouncement: false
  - type: WreckSwarm
    fixedGrid: /Maps/Test/floor3x3.yml
    velocity: 0

- type: ruinMap
  id: TestRuinMap
  mapPath: /Maps/Test/floor3x3.yml

- type: entity
  parent: BaseGameRule
  id: TestWreckSwarmGenerated
  components:
  - type: GameRule
  - type: StationEvent
    duration: 60
    earliestStart: 0
    minimumPlayers: 0
    weight: 0
    globalAnnouncement: false
  - type: WreckSwarm
    chunkConfig: Small
    velocity: 0
";

    #endregion

    #region Tests

    [Test]
    public async Task LaunchAppliesConfiguredRelativeVelocity()
    {
        var ctx = await CreateStationAsync();
        var rule = await StartWreckRuleAsync(ctx.Station, TestLaunchRuleProto);
        var wrecks = Array.Empty<EntityUid>();

        await Pair.Server.WaitAssertion(() =>
        {
            wrecks = GetWrecks(ctx);
            var gridCount = Pair.Server.EntMan.EntityQuery<MapGridComponent>().Count();
            Assert.That(wrecks, Is.Not.Empty,
                $"Expected a wreck to spawn on a clear approach. Grids={gridCount}, rule ended={Pair.Server.EntMan.HasComponent<EndedGameRuleComponent>(rule)}.");
            Assert.That(Pair.Server.EntMan.HasComponent<EndedGameRuleComponent>(rule), Is.True);

            var wreck = wrecks[0];
            var physics = Pair.Server.EntMan.GetComponent<PhysicsComponent>(wreck);
            var wreckPos = Pair.Server.System<SharedTransformSystem>().GetWorldPosition(wreck);
            var stationAabb = Pair.Server.System<SharedPhysicsSystem>().GetWorldAABB(ctx.StationGrid);
            var toStation = stationAabb.Center - wreckPos;

            Assert.Multiple(() =>
            {
                Assert.That(Pair.Server.EntMan.HasComponent<ShuttleComponent>(wreck), Is.False);
                Assert.That(Pair.Server.EntMan.GetComponent<TileFrictionModifierComponent>(wreck).Modifier, Is.EqualTo(0.50f));
                Assert.That(physics.LinearVelocity.Length(), Is.EqualTo(50f).Within(1f));
                Assert.That(Vector2.Dot(physics.LinearVelocity.Normalized(), toStation.Normalized()), Is.GreaterThan(0.9f));
            });
        });
    }

    [Test]
    public async Task BlockedCorridorSelectsAlternateApproach()
    {
        var ctx = await CreateStationAsync();
        var firstPos = Vector2.Zero;
        Box2 firstWreckAabb = default;

        await StartWreckRuleAsync(ctx.Station, TestPlacementRuleProto);
        await Pair.Server.WaitAssertion(() =>
        {
            var wrecks = GetWrecks(ctx);
            Assert.That(wrecks, Is.Not.Empty, "Expected an unblocked wreck so the seeded approach can be recorded.");
            var physics = Pair.Server.System<SharedPhysicsSystem>();
            firstWreckAabb = physics.GetWorldAABB(wrecks[0]);
            firstPos = firstWreckAabb.Center;
            Pair.Server.EntMan.DeleteEntity(wrecks[0]);
        });

        EntityUid blocker = default;
        await Pair.Server.WaitAssertion(() =>
        {
            // Cover the recorded spawn so the same seeded approach intersects this grid.
            var size = Math.Max(16, (int)MathF.Ceiling(MathF.Max(firstWreckAabb.Width, firstWreckAabb.Height)) + 8);
            var origin = firstWreckAabb.Center - new Vector2(size / 2f, size / 2f);
            blocker = CreateFilledGrid(ctx, size, origin);
        });
        await Pair.Server.WaitRunTicks(5);

        await StartWreckRuleAsync(ctx.Station, TestPlacementRuleProto);

        await Pair.Server.WaitAssertion(() =>
        {
            var wrecks = GetWrecks(ctx, extraGrids: [blocker]);
            Assert.That(wrecks, Is.Not.Empty, "A blocked corridor should fall through to another clear approach.");

            var physics = Pair.Server.System<SharedPhysicsSystem>();
            var transform = Pair.Server.System<SharedTransformSystem>();
            var stationAabb = physics.GetWorldAABB(ctx.StationGrid);
            var firstDir = firstPos - stationAabb.Center;
            var blockerAabb = physics.GetWorldAABB(blocker);
            foreach (var wreck in wrecks)
            {
                Assert.That(physics.GetWorldAABB(wreck).Intersects(blockerAabb), Is.False,
                    "The wreck spawned overlapping the corridor blocker.");
                var secondDir = transform.GetWorldPosition(wreck) - stationAabb.Center;
                Assert.That(Vector2.Dot(firstDir.Normalized(), secondDir.Normalized()), Is.LessThan(0.9f),
                    "The wreck reused the blocked approach.");
            }
        });
    }

    [Test]
    public async Task SpawnDoesNotOverlapLooseItemOrGrid()
    {
        var ctx = await CreateStationAsync();
        EntityUid debris = default;
        var sheets = new List<EntityUid>();

        await Pair.Server.WaitAssertion(() =>
        {
            debris = CreateFilledGrid(ctx, 8, new Vector2(80f, -4f));
            var entMan = Pair.Server.EntMan;
            for (var y = -4; y <= 4; y++)
            {
                sheets.Add(entMan.SpawnEntity("SheetSteel1", new MapCoordinates(new Vector2(0f, 100f + y), ctx.MapId)));
            }
        });
        await Pair.Server.WaitRunTicks(5);

        await StartWreckRuleAsync(ctx.Station, TestPlacementRuleProto);

        await Pair.Server.WaitAssertion(() =>
        {
            var wrecks = GetWrecks(ctx, extraGrids: [debris]);
            Assert.That(wrecks, Is.Not.Empty, "Expected a wreck to spawn away from the debris and loose items.");

            var physics = Pair.Server.System<SharedPhysicsSystem>();
            var debrisAabb = physics.GetWorldAABB(debris);

            foreach (var wreck in wrecks)
            {
                var wreckAabb = physics.GetWorldAABB(wreck);
                Assert.That(wreckAabb.Intersects(debrisAabb), Is.False, "Wreck overlapped a blocking grid.");
                foreach (var sheet in sheets)
                {
                    Assert.That(wreckAabb.Intersects(physics.GetWorldAABB(sheet)), Is.False,
                        "Wreck overlapped a loose item.");
                }
            }
        });
    }

    [Test]
    public async Task SpawnSkipsInteriorStationSpace()
    {
        var ctx = await CreateCourtyardStationAsync();

        await Pair.Server.WaitAssertion(() =>
        {
            var wreckSwarm = Pair.Server.System<WreckSwarmSystem>();
            var mapSystem = Pair.Server.System<SharedMapSystem>();
            var grid = Pair.Server.EntMan.GetComponent<MapGridComponent>(ctx.StationGrid);
            var holeCenterTile = new Vector2i((HoleInner + HoleOuter) / 2, (HoleInner + HoleOuter) / 2);
            var holeCenter = mapSystem.GridTileToWorldPos(ctx.StationGrid, grid, holeCenterTile);
            var farPoint = holeCenter + new Vector2(200f, 0f);

            Assert.Multiple(() =>
            {
                Assert.That(wreckSwarm.SpawnBlockedByNearbyGeometry(ctx.MapId, holeCenter), Is.True,
                    "Courtyard hole center should fail nearby-geometry clearance.");
                Assert.That(wreckSwarm.SpawnBlockedByNearbyGeometry(ctx.MapId, farPoint), Is.False,
                    "A point far from the courtyard should be clear.");
            });
        });

        await StartWreckRuleAsync(ctx.Station, TestPlacementRuleProto);

        await Pair.Server.WaitAssertion(() =>
        {
            var wrecks = GetWrecks(ctx);
            Assert.That(wrecks, Is.Not.Empty, "Expected a wreck to spawn outside the station courtyard.");

            var transform = Pair.Server.System<SharedTransformSystem>();
            var holeLocal = new Box2(HoleInner, HoleInner, HoleOuter, HoleOuter);
            var holeWorld = transform.GetWorldMatrix(ctx.StationGrid).TransformBox(holeLocal);

            foreach (var wreck in wrecks)
            {
                var wreckPos = transform.GetWorldPosition(wreck);
                Assert.That(holeWorld.Contains(wreckPos), Is.False,
                    "Wreck spawned in the courtyard hole.");
            }
        });
    }

    [Test]
    public async Task GeneratedRuinSpawnsWreckWithTiles()
    {
        var ctx = await CreateStationAsync();
        await Pair.Server.WaitAssertion(() =>
        {
            var generator = Pair.Server.System<RuinGeneratorSystem>();
            Assert.That(generator.TryCacheMap(new ResPath("/Maps/Test/floor3x3.yml")), Is.True);
        });

        await StartWreckRuleAsync(ctx.Station, TestGeneratedRuleProto);

        await Pair.Server.WaitAssertion(() =>
        {
            var wrecks = GetWrecks(ctx);
            Assert.That(wrecks, Is.Not.Empty, "Expected a generated ruin wreck to spawn.");

            var mapSystem = Pair.Server.System<SharedMapSystem>();
            var grid = Pair.Server.EntMan.GetComponent<MapGridComponent>(wrecks[0]);
            Assert.That(mapSystem.GetAllTiles(wrecks[0], grid).Any(), Is.True,
                "Generated ruin wreck had no tiles.");
        });
    }

    [Test]
    public async Task AllApproachesBlockedEndsWithoutLeakingMaps()
    {
        var ctx = await CreateStationAsync();
        var mapsBefore = 0;
        EntityUid ring = default;

        await Pair.Server.WaitAssertion(() =>
        {
            mapsBefore = Pair.Server.System<SharedMapSystem>().GetAllMapIds().Count();
            ring = CreateRingGrid(ctx, inner: -10, outer: 22);
        });
        await Pair.Server.WaitRunTicks(5);

        var rule = await StartWreckRuleAsync(ctx.Station, TestPlacementRuleProto);

        await Pair.Server.WaitAssertion(() =>
        {
            var mapSystem = Pair.Server.System<SharedMapSystem>();
            var mapsAfter = mapSystem.GetAllMapIds().Count();
            var wrecks = GetWrecks(ctx, extraGrids: [ring]);

            Assert.Multiple(() =>
            {
                Assert.That(Pair.Server.EntMan.HasComponent<EndedGameRuleComponent>(rule), Is.True);
                Assert.That(wrecks, Is.Empty, "A fully blocked approach must fail closed without spawning.");
                Assert.That(mapsAfter, Is.EqualTo(mapsBefore), "The temporary wreck map leaked.");
            });
        });
    }

    #endregion

    #region Helpers

    private sealed class StationContext
    {
        public MapId MapId;
        public EntityUid MapUid;
        public EntityUid Station;
        public EntityUid StationGrid;
        public Tile Tile;
    }

    private async Task<StationContext> CreateStationAsync()
    {
        return await CreateStationAsync(RuinTestHelpers.MakeSquareTiles(StationSize, default));
    }

    private async Task<StationContext> CreateCourtyardStationAsync()
    {
        var ctx = await CreateStationAsync(MakeCourtyardTiles(CourtyardOuter, HoleInner, HoleOuter, default));
        await Pair.Server.WaitAssertion(() =>
        {
            var entMan = Pair.Server.EntMan;
            var mapSystem = Pair.Server.System<SharedMapSystem>();
            var grid = entMan.GetComponent<MapGridComponent>(ctx.StationGrid);

            // Walls on the hole rim so nearby-geometry rays hit Impassable structure, not just floor fixtures.
            for (var x = HoleInner - 1; x <= HoleOuter; x++)
            {
                SpawnWall(ctx, mapSystem, grid, new Vector2i(x, HoleInner - 1));
                SpawnWall(ctx, mapSystem, grid, new Vector2i(x, HoleOuter));
            }

            for (var y = HoleInner; y < HoleOuter; y++)
            {
                SpawnWall(ctx, mapSystem, grid, new Vector2i(HoleInner - 1, y));
                SpawnWall(ctx, mapSystem, grid, new Vector2i(HoleOuter, y));
            }
        });
        await Pair.Server.WaitRunTicks(5);
        return ctx;
    }

    private void SpawnWall(StationContext ctx, SharedMapSystem mapSystem, MapGridComponent grid, Vector2i tile)
    {
        Pair.Server.EntMan.SpawnEntity("WallSolid", mapSystem.GridTileToLocal(ctx.StationGrid, grid, tile));
    }

    private async Task<StationContext> CreateStationAsync(List<(Vector2i Position, Tile Tile)> tiles)
    {
        var map = await Pair.CreateTestMap(true, "FloorSteel");
        var ctx = new StationContext
        {
            MapId = map.MapId,
            MapUid = map.MapUid,
            StationGrid = map.Grid,
            Tile = map.Tile.Tile,
        };

        for (var i = 0; i < tiles.Count; i++)
        {
            tiles[i] = (tiles[i].Position, ctx.Tile);
        }

        await Pair.Server.WaitAssertion(() =>
        {
            var entMan = Pair.Server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            var stationSystem = entMan.System<StationSystem>();
            var grid = entMan.GetComponent<MapGridComponent>(ctx.StationGrid);

            mapSystem.SetTiles(ctx.StationGrid, grid, tiles);
            entMan.EnsureComponent<GridAtmosphereComponent>(ctx.StationGrid);

            ctx.Station = entMan.SpawnEntity(TestStationProto, MapCoordinates.Nullspace);
            stationSystem.AddGridToStation(ctx.Station, ctx.StationGrid);
            Assert.That(entMan.HasComponent<StationDataComponent>(ctx.Station));
        });

        await Pair.Server.WaitRunTicks(10);
        return ctx;
    }

    private async Task<EntityUid> StartWreckRuleAsync(EntityUid station, string ruleProto)
    {
        EntityUid rule = default;
        await Pair.Server.WaitAssertion(() =>
        {
            Pair.Server.ResolveDependency<IRobustRandom>().SetSeed(RandomSeed);
            var ticker = Pair.Server.System<GameTicker>();
            rule = ticker.AddGameRule(ruleProto);
            Pair.Server.EntMan.GetComponent<StationEventComponent>(rule).TargetStation = station;
            Assert.That(ticker.StartGameRule(rule), Is.True);
        });

        await Pair.Server.WaitRunTicks(5);
        return rule;
    }

    private EntityUid[] GetWrecks(StationContext ctx, HashSet<EntityUid> extraGrids = null)
    {
        var wrecks = new List<EntityUid>();
        var query = Pair.Server.EntMan.EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != ctx.MapId || uid == ctx.StationGrid)
                continue;
            if (extraGrids != null && extraGrids.Contains(uid))
                continue;
            if (!Pair.Server.EntMan.HasComponent<TileFrictionModifierComponent>(uid))
                continue;

            wrecks.Add(uid);
        }

        return wrecks.ToArray();
    }

    private EntityUid CreateFilledGrid(StationContext ctx, int sideLength, Vector2 worldPosition)
    {
        var mapMan = Pair.Server.ResolveDependency<IMapManager>();
        var mapSystem = Pair.Server.System<SharedMapSystem>();
        var transform = Pair.Server.System<SharedTransformSystem>();
        var grid = mapMan.CreateGridEntity(ctx.MapId);
        mapSystem.SetTiles(grid.Owner, grid.Comp, RuinTestHelpers.MakeSquareTiles(sideLength, ctx.Tile));
        transform.SetWorldPosition(grid.Owner, worldPosition);
        return grid.Owner;
    }

    private EntityUid CreateRingGrid(StationContext ctx, int inner, int outer)
    {
        var mapMan = Pair.Server.ResolveDependency<IMapManager>();
        var mapSystem = Pair.Server.System<SharedMapSystem>();
        var grid = mapMan.CreateGridEntity(ctx.MapId);
        var tiles = new List<(Vector2i Position, Tile Tile)>();

        for (var x = inner; x <= outer; x++)
        {
            for (var y = inner; y <= outer; y++)
            {
                if (x > inner && x < outer && y > inner && y < outer)
                    continue;

                tiles.Add((new Vector2i(x, y), ctx.Tile));
            }
        }

        mapSystem.SetTiles(grid.Owner, grid.Comp, tiles);
        return grid.Owner;
    }

    private static List<(Vector2i Position, Tile Tile)> MakeCourtyardTiles(int outer, int holeInner, int holeOuter, Tile tile)
    {
        var tiles = new List<(Vector2i Position, Tile Tile)>();
        for (var x = 0; x < outer; x++)
        {
            for (var y = 0; y < outer; y++)
            {
                if (x >= holeInner && x < holeOuter && y >= holeInner && y < holeOuter)
                    continue;

                tiles.Add((new Vector2i(x, y), tile));
            }
        }

        return tiles;
    }

    #endregion
}
