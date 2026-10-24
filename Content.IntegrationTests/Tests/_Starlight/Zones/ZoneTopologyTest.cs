#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Zones;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.Zones;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Zones;

[TestFixture]
[TestOf(typeof(ZoneSystem))]
public sealed class ZoneTopologyTest : GameTest
{
    private static readonly EntProtoId _wall = "WallSolid";
    private static readonly EntProtoId _door = "Airlock";

    private const int Width = 8;
    private const int Height = 4;
    private const int WallColumn = 4;

    private ZoneSystem _zones = default!;
    private EntityUid _grid;

    /// <summary>
    /// Tests that a wall cutting across a room splits it into two, and that removing the wall merges the room back together.
    /// </summary>
    [Test]
    public async Task WallSplitsRoomAndRemovingItMergesBack()
    {
        await CreateDeck();

        var maintenance = _zones.GetZoneId("Maintenance");
        var hallway = _zones.GetZoneId("Hallway");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Region(0, 0), Is.Not.EqualTo(SharedZoneSystem.NoRegion));
            Assert.That(Region(0, 0), Is.EqualTo(Region(Width - 1, 0)),
                "Open deck should be a single room.");
            Assert.That(Zone(Width - 1, 0), Is.EqualTo(maintenance),
                "The higher priority half should name the whole room.");
        }

        var walls = await BuildColumn(_wall);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Region(0, 0), Is.Not.EqualTo(Region(Width - 1, 0)),
                "A wall across the deck should have cut it in two.");
            Assert.That(Zone(0, 0), Is.EqualTo(maintenance));
            Assert.That(Zone(Width - 1, 0), Is.EqualTo(hallway),
                "The cut off half should be renamed after its own tiles.");
        }

        await Server.WaitPost(() =>
        {
            foreach (var wall in walls)
            {
                Server.EntMan.DeleteEntity(wall);
            }
        });

        await Server.WaitRunTicks(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Region(0, 0), Is.EqualTo(Region(Width - 1, 0)),
                "Removing the wall should have put the room back together.");
            Assert.That(Zone(Width - 1, 0), Is.EqualTo(maintenance),
                "The merged room should take the higher priority name again.");
        }
    }

    /// <summary>
    /// Tests that a line of doors splits a room, but opening the doors does not merge the rooms back together.
    /// </summary>
    [Test]
    public async Task DoorSplitsRoomButCyclingItDoesNot()
    {
        await CreateDeck();

        var doors = await BuildColumn(_door);

        var left = Region(0, 0);
        var right = Region(Width - 1, 0);

        Assert.That(left, Is.Not.EqualTo(right), "A line of doors should separate two rooms.");

        await Server.WaitPost(() =>
        {
            var airtight = Server.System<AirtightSystem>();

            foreach (var door in doors)
            {
                if (Server.EntMan.TryGetComponent(door, out AirtightComponent? comp))
                    airtight.SetAirblocked((door, comp), false);
            }
        });

        await Server.WaitRunTicks(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Region(0, 0), Is.EqualTo(left),
                "Opening a door must not disturb the rooms either side of it.");
            Assert.That(Region(Width - 1, 0), Is.EqualTo(right));
        }

        await Server.WaitPost(() =>
        {
            var airtight = Server.System<AirtightSystem>();

            foreach (var door in doors)
            {
                if (Server.EntMan.TryGetComponent(door, out AirtightComponent? comp))
                    airtight.SetAirblocked((door, comp), true);
            }
        });

        await Server.WaitRunTicks(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Region(0, 0), Is.EqualTo(left),
                "Closing the doors again must not change the existing left room.");
            Assert.That(Region(Width - 1, 0), Is.EqualTo(right),
                "Closing the doors again must not change the existing right room.");
        }
    }

    /// <summary>
    /// Tests that a small closet can be cut off from the rest of the deck and become its own room.
    /// </summary>
    [Test]
    public async Task ClosetBecomesItsOwnRoom()
    {
        await CreateDeck();

        var deck = Region(Width - 1, 0);

        await Server.WaitPost(() =>
        {
            SpawnAnchored(_wall, new Vector2i(5, 0));
            SpawnAnchored(_wall, new Vector2i(6, 1));
            SpawnAnchored(_wall, new Vector2i(7, 1));
        });

        await Server.WaitRunTicks(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Region(7, 0), Is.Not.EqualTo(SharedZoneSystem.NoRegion));
            Assert.That(Region(7, 0), Is.EqualTo(Region(6, 0)),
                "Both tiles of the closet belong to the same room.");
            Assert.That(Region(7, 0), Is.Not.EqualTo(deck),
                "The closet is no longer part of the deck.");
            Assert.That(Region(0, 0), Is.EqualTo(deck),
                "The far side of the deck should have been left alone.");
        }
    }

    private ushort Region(int x, int y) => _zones.GetRegion(_grid, new Vector2i(x, y));

    private ushort Zone(int x, int y) => _zones.GetZoneId(_grid, new Vector2i(x, y));

    private async Task CreateDeck()
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

            comp.Shapes =
            [
                new ZoneShapeSet { Zone = "Maintenance", Rects = [new Box2i(0, 0, WallColumn, Height)] },
                new ZoneShapeSet { Zone = "Hallway", Rects = [new Box2i(WallColumn, 0, Width, Height)] },
            ];

            _zones.RebuildHints((_grid, comp));
            _zones.FullRebuild((_grid, comp));
        });
    }

    private async Task<List<EntityUid>> BuildColumn(EntProtoId proto)
    {
        var spawned = new List<EntityUid>();

        await Server.WaitPost(() =>
        {
            for (var y = 0; y < Height; y++)
            {
                spawned.Add(SpawnAnchored(proto, new Vector2i(WallColumn, y)));
            }
        });

        await Server.WaitRunTicks(5);

        return spawned;
    }

    private EntityUid SpawnAnchored(EntProtoId proto, Vector2i tile)
    {
        var entMan = Server.EntMan;
        var uid = entMan.SpawnEntity(proto, new EntityCoordinates(_grid, tile.X + 0.5f, tile.Y + 0.5f));

        var xform = entMan.GetComponent<TransformComponent>(uid);
        if (!xform.Anchored)
            entMan.System<SharedTransformSystem>().AnchorEntity((uid, xform));

        return uid;
    }
}
