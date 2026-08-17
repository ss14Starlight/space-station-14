using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Components;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
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

namespace Content.IntegrationTests.Tests._Starlight.StationEvents;

[TestFixture]
[TestOf(typeof(WreckSwarmSystem))]
public sealed class WreckSwarmLaunchTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    #region Prototypes

    private const string TestStationProto = "TestWreckSwarmStation";
    private const string TestRuleProto = "TestWreckSwarmLaunch";
    private const int StationSize = 12;
    private const int RandomSeed = 534;

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
";

    #endregion

    #region Tests

    [Test]
    public async Task LaunchAppliesConfiguredRelativeVelocity()
    {
        var ctx = await CreateStationAsync();
        var rule = await StartWreckRuleAsync(ctx.Station);
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
                Assert.That(Pair.Server.EntMan.GetComponent<TileFrictionModifierComponent>(wreck).Modifier, Is.EqualTo(0.25f));
                Assert.That(physics.LinearVelocity.Length(), Is.EqualTo(50f).Within(1f));
                Assert.That(Vector2.Dot(physics.LinearVelocity.Normalized(), toStation.Normalized()), Is.GreaterThan(0.9f));
            });
        });
    }

    [Test]
    public async Task BlockedCorridorSelectsAlternateApproach()
    {
        var ctx = await CreateStationAsync();
        EntityUid blocker = default;

        await Pair.Server.WaitAssertion(() =>
        {
            blocker = CreateFilledGrid(ctx, 10, new Vector2(90f, -5f));
        });
        await Pair.Server.WaitRunTicks(5);

        await StartWreckRuleAsync(ctx.Station);

        await Pair.Server.WaitAssertion(() =>
        {
            var wrecks = GetWrecks(ctx, extraGrids: [blocker]);
            Assert.That(wrecks, Is.Not.Empty, "A blocked corridor should fall through to another clear approach.");

            var physics = Pair.Server.System<SharedPhysicsSystem>();
            var blockerAabb = physics.GetWorldAABB(blocker);
            foreach (var wreck in wrecks)
            {
                Assert.That(physics.GetWorldAABB(wreck).Intersects(blockerAabb), Is.False,
                    "The wreck spawned overlapping the corridor blocker.");
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

        await StartWreckRuleAsync(ctx.Station);

        await Pair.Server.WaitAssertion(() =>
        {
            var wrecks = GetWrecks(ctx, extraGrids: [debris]);
            Assert.That(wrecks, Is.Not.Empty, "Expected a wreck to spawn away from the debris and loose items.");

            var physics = Pair.Server.System<SharedPhysicsSystem>();
            var transform = Pair.Server.System<SharedTransformSystem>();
            var debrisAabb = physics.GetWorldAABB(debris);

            foreach (var wreck in wrecks)
            {
                var wreckAabb = physics.GetWorldAABB(wreck);
                Assert.That(wreckAabb.Intersects(debrisAabb), Is.False, "Wreck overlapped a blocking grid.");
                foreach (var sheet in sheets)
                {
                    Assert.That(wreckAabb.Contains(transform.GetWorldPosition(sheet)), Is.False,
                        "Wreck overlapped a loose item.");
                }
            }
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

        var rule = await StartWreckRuleAsync(ctx.Station);

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
        var map = await Pair.CreateTestMap(true, "FloorSteel");
        var ctx = new StationContext
        {
            MapId = map.MapId,
            MapUid = map.MapUid,
            StationGrid = map.Grid,
            Tile = map.Tile.Tile,
        };

        await Pair.Server.WaitAssertion(() =>
        {
            var entMan = Pair.Server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            var stationSystem = entMan.System<StationSystem>();
            var grid = entMan.GetComponent<MapGridComponent>(ctx.StationGrid);

            mapSystem.SetTiles(ctx.StationGrid, grid, MakeSquareTiles(StationSize, ctx.Tile));
            entMan.EnsureComponent<GridAtmosphereComponent>(ctx.StationGrid);

            ctx.Station = entMan.SpawnEntity(TestStationProto, MapCoordinates.Nullspace);
            stationSystem.AddGridToStation(ctx.Station, ctx.StationGrid);
            Assert.That(entMan.HasComponent<StationDataComponent>(ctx.Station));
        });

        await Pair.Server.WaitRunTicks(10);
        return ctx;
    }

    private async Task<EntityUid> StartWreckRuleAsync(EntityUid station)
    {
        EntityUid rule = default;
        await Pair.Server.WaitAssertion(() =>
        {
            Pair.Server.ResolveDependency<IRobustRandom>().SetSeed(RandomSeed);
            var ticker = Pair.Server.System<GameTicker>();
            rule = ticker.AddGameRule(TestRuleProto);
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
        mapSystem.SetTiles(grid.Owner, grid.Comp, MakeSquareTiles(sideLength, ctx.Tile));
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

    private static List<(Vector2i Position, Tile Tile)> MakeSquareTiles(int sideLength, Tile tile)
    {
        var tiles = new List<(Vector2i Position, Tile Tile)>(sideLength * sideLength);
        for (var x = 0; x < sideLength; x++)
        {
            for (var y = 0; y < sideLength; y++)
            {
                tiles.Add((new Vector2i(x, y), tile));
            }
        }

        return tiles;
    }

    #endregion
}
