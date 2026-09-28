using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Zones;
using Content.Shared._Starlight.Zones;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Starlight.Zones;

[TestFixture]
[TestOf(typeof(ZoneSystem))]
public sealed class ZoneEditorTest : GameTest
{
    private ZoneSystem _zones = default!;
    private Entity<ZoneGridComponent> _grid;

    [Test]
    public async Task PaintingTheSamePatchTwiceDoesNotPileUpRectangles()
    {
        await CreateGrid();

        await Server.WaitPost(() =>
        {
            _zones.PaintZoneRect(_grid, new Box2i(0, 0, 10, 10), "Cargo");
            _zones.PaintZoneRect(_grid, new Box2i(2, 2, 6, 6), "Cargo");
        });

        var cargo = _zones.GetZoneId("Cargo");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Zone(0, 0), Is.EqualTo(cargo));
            Assert.That(Zone(4, 4), Is.EqualTo(cargo));
            Assert.That(Zone(9, 9), Is.EqualTo(cargo));

            Assert.That(Rects("Cargo").Count(rect => Contains(rect, 4, 4)), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task NeighbouringRectanglesBecomeOne()
    {
        await CreateGrid();

        await Server.WaitPost(() =>
        {
            _zones.PaintZoneRect(_grid, new Box2i(0, 0, 5, 5), "Cargo");
            _zones.PaintZoneRect(_grid, new Box2i(5, 0, 10, 5), "Cargo");
            _zones.PaintZoneRect(_grid, new Box2i(0, 5, 5, 10), "Cargo");
            _zones.PaintZoneRect(_grid, new Box2i(5, 5, 10, 10), "Cargo");
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Rects("Cargo"), Has.Count.EqualTo(1),
                "Four squares that tile a bigger square should end up as one rectangle.");
            Assert.That(Rects("Cargo")[0], Is.EqualTo(new Box2i(0, 0, 10, 10)));
        }
    }

    [Test]
    public async Task RectanglesThatDoNotLineUpStayApart()
    {
        await CreateGrid();

        await Server.WaitPost(() =>
        {
            _zones.PaintZoneRect(_grid, new Box2i(0, 0, 5, 5), "Cargo");
            _zones.PaintZoneRect(_grid, new Box2i(5, 0, 10, 8), "Cargo");
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Rects("Cargo"), Has.Count.EqualTo(2));
            Assert.That(Zone(2, 6), Is.EqualTo(SharedZoneSystem.NoZone),
                "Merging must not swallow tiles that were never painted.");
        }
    }

    [Test]
    public async Task ErasingTakesABiteOutOfARectangle()
    {
        await CreateGrid();

        await Server.WaitPost(() =>
        {
            _zones.PaintZoneRect(_grid, new Box2i(0, 0, 10, 10), "Cargo");
            _zones.EraseZoneRect(_grid, new Box2i(4, 4, 6, 6));
        });

        var cargo = _zones.GetZoneId("Cargo");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Zone(4, 4), Is.EqualTo(SharedZoneSystem.NoZone), "The hole should be empty.");
            Assert.That(Zone(5, 5), Is.EqualTo(SharedZoneSystem.NoZone));

            Assert.That(Zone(3, 4), Is.EqualTo(cargo), "The ring around the hole should survive.");
            Assert.That(Zone(6, 4), Is.EqualTo(cargo));
            Assert.That(Zone(4, 3), Is.EqualTo(cargo));
            Assert.That(Zone(4, 6), Is.EqualTo(cargo));
            Assert.That(Zone(0, 0), Is.EqualTo(cargo));
            Assert.That(Zone(9, 9), Is.EqualTo(cargo));

            Assert.That(Rects("Cargo"), Has.Count.EqualTo(4));
        }
    }

    [Test]
    public async Task ErasingEverythingRemovesTheShape()
    {
        await CreateGrid();

        await Server.WaitPost(() =>
        {
            _zones.PaintZoneRect(_grid, new Box2i(0, 0, 10, 10), "Cargo");
            _zones.EraseZoneRect(_grid, new Box2i(-4, -4, 20, 20));
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Zone(5, 5), Is.EqualTo(SharedZoneSystem.NoZone));
            Assert.That(_grid.Comp.Shapes, Is.Empty);
        }
    }

    [Test]
    public async Task PaintingAnotherZoneReplacesWhatWasThere()
    {
        await CreateGrid();

        await Server.WaitPost(() =>
        {
            _zones.PaintZoneRect(_grid, new Box2i(0, 0, 10, 10), "Hallway");
            _zones.PaintZoneRect(_grid, new Box2i(4, 4, 6, 6), "Maintenance");
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Zone(5, 5), Is.EqualTo(_zones.GetZoneId("Maintenance")),
                "The newer zone should own the tiles it was painted over.");
            Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Hallway")),
                "The rest of the older zone should be untouched.");

            Assert.That(Rects("Hallway").Count(rect => Contains(rect, 5, 5)), Is.Zero);
            Assert.That(Rects("Hallway"), Has.Count.EqualTo(4));
        }
    }

    [Test]
    public async Task PaintingOverAZoneEntirelyRemovesIt()
    {
        await CreateGrid();

        await Server.WaitPost(() =>
        {
            _zones.PaintZoneRect(_grid, new Box2i(2, 2, 6, 6), "Hallway");
            _zones.PaintZoneRect(_grid, new Box2i(0, 0, 10, 10), "Maintenance");
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Zone(4, 4), Is.EqualTo(_zones.GetZoneId("Maintenance")));
            Assert.That(Rects("Hallway"), Is.Empty, "The buried zone should be gone entirely.");
        }
    }

    #region Helpers

    private ushort Zone(int x, int y) => _zones.GetZoneId(_grid.Owner, new Vector2i(x, y));

    private List<Box2i> Rects(string zone)
        => _grid.Comp.Shapes.Find(set => set.Zone == zone)?.Rects ?? new List<Box2i>();

    private static bool Contains(Box2i rect, int x, int y)
        => x >= rect.Left && x < rect.Right && y >= rect.Bottom && y < rect.Top;

    private async Task CreateGrid()
    {
        var entMan = Server.EntMan;
        var mapSys = entMan.System<SharedMapSystem>();

        _zones = entMan.System<ZoneSystem>();

        var testMap = await Pair.CreateTestMap();

        await Server.WaitPost(() =>
        {
            var grid = mapSys.CreateGridEntity(testMap.MapId);
            _grid = (grid.Owner, entMan.AddComponent<ZoneGridComponent>(grid.Owner));
        });
    }

    #endregion
}
