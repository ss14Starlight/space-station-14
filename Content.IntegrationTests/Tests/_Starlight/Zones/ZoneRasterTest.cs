using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Zones;
using Content.Shared._Starlight.Zones;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Starlight.Zones;

[TestFixture]
[TestOf(typeof(SharedZoneSystem))]
public sealed class ZoneRasterTest : GameTest
{
    [Test]
    public async Task RasterisesShapes()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var mapSys = entMan.System<SharedMapSystem>();
        var zones = entMan.System<ZoneSystem>();

        var testMap = await Pair.CreateTestMap();

        Entity<MapGridComponent> grid = default;

        await server.WaitPost(() =>
        {
            grid = mapSys.CreateGridEntity(testMap.MapId);

            var comp = entMan.AddComponent<ZoneGridComponent>(grid);

            comp.Shapes =
            [

                new ZoneShapeSet
                {
                    Zone = "Bar",
                    Circles = [new ZoneCircle { Center = new Vector2(20.5f, 20.5f), Radius = 2f }],
                },

                new ZoneShapeSet
                {
                    Zone = "Cargo",
                    Rects = [new Box2i(-8, -8, -4, -4)],
                },

                new ZoneShapeSet
                {
                    Zone = "Maintenance",
                    Rects = [new Box2i(4, 4, 8, 8)],
                },

                new ZoneShapeSet
                {
                    Zone = "Hallway",
                    Rects = [new Box2i(0, 0, 16, 16)],
                },
            ];

            zones.RebuildHints((grid.Owner, comp));
            zones.FullRebuild((grid.Owner, comp));
        });

        var hallway = zones.GetZoneId("Hallway");
        var maintenance = zones.GetZoneId("Maintenance");
        var cargo = zones.GetZoneId("Cargo");
        var bar = zones.GetZoneId("Bar");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hallway, Is.Not.EqualTo(SharedZoneSystem.NoZone), "Zone prototypes did not get ids.");

            Assert.That(Zone(0, 0), Is.EqualTo(hallway));
            Assert.That(Zone(15, 15), Is.EqualTo(hallway));
            Assert.That(Zone(16, 0), Is.EqualTo(SharedZoneSystem.NoZone), "Right edge should be exclusive.");
            Assert.That(Zone(0, 16), Is.EqualTo(SharedZoneSystem.NoZone), "Top edge should be exclusive.");

            Assert.That(Zone(4, 4), Is.EqualTo(maintenance));
            Assert.That(Zone(7, 7), Is.EqualTo(maintenance));
            Assert.That(Zone(3, 4), Is.EqualTo(hallway));
            Assert.That(Zone(8, 8), Is.EqualTo(hallway));

            Assert.That(Zone(-8, -8), Is.EqualTo(cargo));
            Assert.That(Zone(-5, -5), Is.EqualTo(cargo));
            Assert.That(Zone(-4, -4), Is.EqualTo(SharedZoneSystem.NoZone));
            Assert.That(Zone(-9, -8), Is.EqualTo(SharedZoneSystem.NoZone));

            Assert.That(Zone(20, 20), Is.EqualTo(bar));
            Assert.That(Zone(19, 20), Is.EqualTo(bar));
            Assert.That(Zone(23, 20), Is.EqualTo(SharedZoneSystem.NoZone));
            Assert.That(Zone(20, 23), Is.EqualTo(SharedZoneSystem.NoZone));

            Assert.That(Zone(7, 7), Is.EqualTo(maintenance));
            Assert.That(Zone(8, 8), Is.EqualTo(hallway));
            Assert.That(Zone(7, 7), Is.EqualTo(maintenance));
            Assert.That(Zone(-5, -5), Is.EqualTo(cargo));
            Assert.That(Zone(8, 8), Is.EqualTo(hallway));
        }

        return;

        ushort Zone(int x, int y) => zones.GetZoneId(grid.Owner, new Vector2i(x, y));
    }

    [Test]
    public async Task EmptyGridHasNoZones()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var mapSys = entMan.System<SharedMapSystem>();
        var zones = entMan.System<ZoneSystem>();

        var testMap = await Pair.CreateTestMap();

        Entity<MapGridComponent> grid = default;

        await server.WaitPost(() =>
        {
            grid = mapSys.CreateGridEntity(testMap.MapId);
            mapSys.SetTile(grid, grid, Vector2i.Zero, new Tile(1));
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(zones.GetZoneId(grid.Owner, Vector2i.Zero), Is.EqualTo(SharedZoneSystem.NoZone));
            Assert.That(zones.TryGetZone(grid.Owner, Vector2i.Zero, out _), Is.False);
        }
    }
}
