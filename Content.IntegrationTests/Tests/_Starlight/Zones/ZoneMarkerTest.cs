using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Zones;
using Content.Shared._Starlight.Zones;
using Content.Shared.SprayPainter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Zones;

[TestFixture]
[TestOf(typeof(ZoneSystem))]
public sealed class ZoneMarkerTest : GameTest
{
    private static readonly EntProtoId _maintDoor = "AirlockMaint";
    private static readonly EntProtoId _firelock = "Firelock";
    private static readonly EntProtoId _cargoDoor = "AirlockCargo";

    private const int Width = 8;
    private const int Height = 4;

    private ZoneSystem _zones = default!;
    private EntityUid _grid;

    /// <summary>
    /// This test is a bit of a sanity check for the zone system.
    /// It ensures that if you have a room with no shapes, the only thing that can name it is a door.
    /// This is important because the mapper may not have drawn any shapes, and we want to make sure that the zone system can still function in that case.
    /// </summary>
    [Test]
    public async Task DoorNamesAnUnmappedRoom()
    {
        await CreateDeck(shapes: false);

        Assert.That(Zone(0, 0), Is.EqualTo(SharedZoneSystem.NoZone), "Bare deck should have no name yet.");

        await Build(_maintDoor, new Vector2i(4, 2));

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Maintenance")),
            "A maintenance door is the only thing saying what this place is.");
    }

    /// <summary>
    /// This test ensures that if you have a room with shapes, the shapes will take precedence over any doors that are placed in the room.
    /// </summary>
    [Test]
    public async Task ShapesBeatDoors()
    {
        await CreateDeck(shapes: true);

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Hallway")));

        await Build(_maintDoor, new Vector2i(4, 2));

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Maintenance")),
            "A higher priority door should be able to rename a room the mapper drew.");
    }

    /// <summary>
    /// This test ensures that if you have a room with shapes, and you place a door that has a different name than the shape, the shape will take precedence over the door.
    /// </summary>
    [Test]
    public async Task DoorsBeatShapesWhateverTheyMean()
    {
        await CreateDeck(shapes: true, zone: "Atmospherics");

        await Build(_maintDoor, new Vector2i(4, 2));

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Maintenance")),
            "The door names the room even where the mapper drew something more specific.");
    }

    /// <summary>
    /// This test ensures that if you have a room with shapes, and you place a door that has the same name as the shape, the shape will take precedence over the door.
    /// </summary>
    [Test]
    public async Task ShapesStandWhenNothingContradictsThem()
    {
        await CreateDeck(shapes: true, zone: "Atmospherics");

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Atmospherics")));

        await Build(new EntProtoId("Airlock"), new Vector2i(4, 2));

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Atmospherics")),
            "A door with no department to it should leave the shape alone.");
    }

    /// <summary>
    /// This test ensures that if you have a room with shapes, and you place two doors that have different names than the shape, the shape will take precedence over the doors.
    /// </summary>
    [Test]
    public async Task DoorsThatDisagreeAreIgnored()
    {
        await CreateDeck(shapes: true);

        await Build(_maintDoor, new Vector2i(2, 2));
        await Build(_cargoDoor, new Vector2i(6, 2));

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Hallway")),
            "Doors that do not agree should leave the room to whatever the mapper drew.");
    }

    /// <summary>
    /// This test ensures that if you have a room with shapes, and you place a door that has a different name than the shape, and then you repaint the door to have the same name as the shape, the shape will take precedence over the door.
    /// </summary>
    [Test]
    public async Task RepaintingBackDownReleasesTheRoom()
    {
        await CreateDeck(shapes: true);

        var doors = await Build(_maintDoor, new Vector2i(4, 2));

        await Paint(doors[0], "AirlockCargo");

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Cargo")));

        await Paint(doors[0], "Airlock");

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Hallway")),
            "A door that no longer means anything should give the room back to the shape.");
    }

    /// <summary>
    /// This test ensures that if you have a room with shapes, and you place a door that has a different name than the shape, and then you remove the door, the shape will take precedence over the door.
    /// </summary>
    [Test]
    public async Task RemovingTheDoorReleasesTheRoom()
    {
        await CreateDeck(shapes: true);

        var doors = await Build(_maintDoor, new Vector2i(4, 2));

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Maintenance")));

        await Server.WaitPost(() => Server.EntMan.DeleteEntity(doors[0]));
        await Server.WaitRunTicks(10);

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Hallway")),
            "With the door gone there is nothing saying maintenance any more.");
    }

    /// <summary>
    /// This test ensures that if you have a room with shapes, and you place a door that has a different name than the shape, and then you place a marker that has the same name as the shape, the marker will take precedence over the door.
    /// </summary>
    [Test]
    public async Task PlacedMarkersBeatDoors()
    {
        await CreateDeck(shapes: true);

        await Build(_maintDoor, new Vector2i(4, 2));
        var markers = await Build(new EntProtoId("ZoneMarkerSolars"), new Vector2i(1, 1));

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Solars")),
            "A marker somebody placed should out-rank a door that happens to be there.");

        await Server.WaitPost(() => Server.EntMan.DeleteEntity(markers[0]));
        await Server.WaitRunTicks(10);

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Maintenance")),
            "After the marker is removed, the room should fall back to the maintenance door.");
    }

    /// <summary>
    /// This test ensures that if you have a room with shapes, and you place a door that has a different name than the shape, and then you repaint the door to have the same name as the shape, the shape will take precedence over the door.
    /// </summary>
    [Test]
    public async Task RepaintingADoorRenamesTheRoom()
    {
        await CreateDeck(shapes: false);

        var doors = await Build(_maintDoor, new Vector2i(4, 2));

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Maintenance")));

        await Server.WaitPost(() =>
        {
            var ev = new EntityPaintedEvent(null, doors[0], "AirlockCargo", "AirlockStandard");
            Server.EntMan.EventBus.RaiseLocalEvent(doors[0], ref ev);
        });

        await Server.WaitRunTicks(5);

        Assert.That(Zone(0, 0), Is.EqualTo(_zones.GetZoneId("Cargo")),
            "The room should follow the paint on the door that named it.");
    }

    /// <summary>
    /// This test ensures that firelocks do not act as boundaries for zones, meaning they should not split a room into separate zones.
    /// </summary>
    [Test]
    public async Task FirelocksAreNotBoundaries()
    {
        await CreateDeck(shapes: true);

        var before = Region(0, 0);

        await Server.WaitPost(() =>
        {
            for (var y = 0; y < Height; y++)
            {
                Spawn(_firelock, new Vector2i(4, y));
            }
        });

        await Server.WaitRunTicks(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Region(0, 0), Is.EqualTo(Region(Width - 1, 0)),
                "A line of firelocks should not have split the corridor.");
            Assert.That(Region(0, 0), Is.EqualTo(before),
                "It should not have disturbed the room at all.");
        }
    }

    #region Helpers

    private ushort Region(int x, int y) => _zones.GetRegion(_grid, new Vector2i(x, y));

    private ushort Zone(int x, int y) => _zones.GetZoneId(_grid, new Vector2i(x, y));

    private async Task CreateDeck(bool shapes, string zone = "Hallway")
    {
        var entMan = Server.EntMan;
        var mapSys = entMan.System<SharedMapSystem>();
        var tileDefs = Server.ResolveDependency<ITileDefinitionManager>();

        _zones = entMan.System<ZoneSystem>();

        var testMap = await Pair.CreateTestMap();

        await Server.WaitPost(() =>
        {
            var grid = mapSys.CreateGridEntity(testMap.MapId);
            _grid = grid.Owner;

            var plating = new Tile(tileDefs["Plating"].TileId);

            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
                mapSys.SetTile(grid, grid, new Vector2i(x, y), plating);

            var comp = entMan.AddComponent<ZoneGridComponent>(_grid);

            if (shapes)
            {
                comp.Shapes =
                [
                    new ZoneShapeSet { Zone = zone, Rects = [new Box2i(0, 0, Width, Height)] },
                ];
            }

            _zones.RebuildHints((_grid, comp));
            _zones.FullRebuild((_grid, comp));
        });
    }

    private async Task<List<EntityUid>> Build(EntProtoId proto, params Vector2i[] tiles)
    {
        var spawned = new List<EntityUid>();

        await Server.WaitPost(() =>
        {
            foreach (var tile in tiles)
            {
                spawned.Add(Spawn(proto, tile));
            }
        });

        await Server.WaitRunTicks(5);

        return spawned;
    }

    private async Task Paint(EntityUid uid, EntProtoId style)
    {
        await Server.WaitPost(() =>
        {
            var ev = new EntityPaintedEvent(null, uid, style, "AirlockStandard");
            Server.EntMan.EventBus.RaiseLocalEvent(uid, ref ev);
        });

        await Server.WaitRunTicks(10);
    }

    private EntityUid Spawn(EntProtoId proto, Vector2i tile)
    {
        var entMan = Server.EntMan;
        var uid = entMan.SpawnEntity(proto, new EntityCoordinates(_grid, tile.X + 0.5f, tile.Y + 0.5f));

        var xform = entMan.GetComponent<TransformComponent>(uid);
        if (!xform.Anchored)
            entMan.System<SharedTransformSystem>().AnchorEntity((uid, xform));

        return uid;
    }

    #endregion
}
