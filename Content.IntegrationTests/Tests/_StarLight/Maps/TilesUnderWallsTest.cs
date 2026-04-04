using System.Collections.Generic;
using Content.Server.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests._Starlight.Maps;

[TestFixture]
public sealed class MapWallFloorTests
{
    /// <summary>
    /// The acceptable floor tiles that are allowed to exist underneath a wall.
    /// Add any custom hull plating IDs here.
    /// </summary>
    private static readonly HashSet<string> AllowedUnderWalls = new()
    {
        "Plating",
        "Lattice"
    };

    /// <summary>
    /// The wall types that are allowed to have tiles underneath.
    /// </summary>
    private static readonly HashSet<string> AllowedWalls = new()
    {
        "AsteroidRock",
        "AsteroidRockArtifactFragment",
        "AsteroidRockBananium",
        "AsteroidRockBananiumCrab",
        "AsteroidRockBluespace",
        "AsteroidRockCoal",
        "AsteroidRockCoalCrab",
        "AsteroidRockDiamond",
        "AsteroidRockGibtonite",
        "AsteroidRockGold",
        "AsteroidRockGoldCrab",
        "AsteroidRockTin",
        "AsteroidRockTinCrab",
        "AsteroidRockPlasma",
        "AsteroidRockQuartz",
        "AsteroidRockQuartzCrab",
        "AsteroidRockSalt",
        "AsteroidRockSilver",
        "AsteroidRockSilverCrab",
        "AsteroidRockUranium",
        "AsteroidRockUraniumCrab",
        "AsteroidRockMining",
        "WoodenSupportWall",
        "WoodenSupportWallBroken",
        "SolidSecretDoor"
    };

    private static readonly string[] GameMaps =
    [
        "StarlightBarratry",
        "StarlightCork",
        "StarlightKiloton",
        "StarlightLagan",
        "StarlightLobster",
        "StarlightManor",
        "StarlightLeth",
        "StarlightMing",
        "StarlightOrwell",
        "StarlightPrism",
        "StarlightStarboard",
        "StarlightBagel",
        "StarlightBox",
        "StarlightCentCommG24",
        "StarlightCentCommSC17",
        "StarlightCentCommGNT9",
        "StarlightCog",
        "StarlightElkridge",
        "StarlightFland",
        "StarlightHotel",
        "StarlightOasis",
        "StarlightPacked",
        "StarlightReach",
        "StarlightSaltern",
        "StarlightSilica",
        "StarlightSpaceMall",
        "StarlightCluster",
        "StarlightStationBuilding",
        "StarlightPlasma",
        "StarlightSepultum",
        "StarlightBoxcars"
    ];

    [Test, TestCaseSource(nameof(GameMaps))]
    public async Task TestWallsHaveNoFloors(string mapProtoId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var ticker = entMan.System<GameTicker>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var tagSystem = entMan.System<TagSystem>();
        var tileDefMan = server.ResolveDependency<ITileDefinitionManager>();

        MapId mapId = MapId.Nullspace;

        // Load the map
        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.TryIndex<GameMapPrototype>(mapProtoId, out var mapProto));
            var opts = DeserializationOptions.Default with { InitializeMaps = true };
            ticker.LoadGameMap(mapProto, out mapId, opts);
        });

        await server.WaitAssertion(() =>
        {
            var errors = new List<string>();
            var walls = 0;
            var entities = 0;

            // Query all entities to find walls
            var query = entMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out var xform, out var meta))
            {
                entities += 1;

                // We only care about anchored entities on the map we just loaded
                if (xform.MapID != mapId || !xform.Anchored || xform.GridUid == null)
                    continue;

                // Identify if the entity is a Wall.
                // Checks the standard "Wall" tag, excludes Diagonal walls because they should have tiles under them.
                var isWall = tagSystem.HasTag(uid, "Wall") && !tagSystem.HasTag(uid, "Diagonal") && !AllowedWalls.Contains(meta.EntityPrototype?.ID);

                if (!isWall)
                    continue;

                walls += 1;

                // Get the grid component so we can check the tile underneath
                if (!entMan.TryGetComponent<MapGridComponent>(xform.GridUid.Value, out var gridComp))
                    continue;

                var tileRef = mapSystem.GetTileRef(xform.GridUid.Value, gridComp, xform.Coordinates);
                var tileDef = tileDefMan[tileRef.Tile.TypeId];

                // Check if the underlying tile is a valid hull plate
                if (!AllowedUnderWalls.Contains(tileDef.ID))
                {
                    var tileIndices = mapSystem.LocalToTile(xform.GridUid.Value, gridComp, xform.Coordinates);
                    var gridName = entMan.TryGetComponent<MetaDataComponent>(xform.GridUid.Value, out var parentGridMeta) ? parentGridMeta.EntityName : "UnknownGrid";
                    errors.Add($"[{mapProtoId}] Wall {meta.EntityPrototype?.ID} on grid {gridName} " +
                               $"at coordinates {tileIndices} is placed on a floor '{tileDef.ID}'. ");
                               // If your coordinates don't land you onto the wall with the problem, tp your grid to 0 0 first.
                }
            }

            Console.WriteLine($"Found {entities} entities and {walls} walls.");

            if (errors.Count > 0)
            {
                Assert.Fail(string.Join("\n", errors));
            }

            // Assert.Multiple(() =>
            // {
            //     foreach (var error in errors)
            //     {
            //         Assert.Fail(error);
            //     }
            // });
        });

        await pair.CleanReturnAsync();
    }
}
