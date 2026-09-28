#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Starlight.Zones;
using Content.Shared.CCVar;
using Content.Shared._Starlight.Zones;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Starlight.Zones;

[TestFixture]
[TestOf(typeof(ZoneGridComponent))]
public sealed class ZoneSaveLoadTest : GameTest
{
    private static readonly Box2i _cargoRect = new(3, -5, 11, 7);
    private static readonly Box2i _maintRect = new(5, -1, 8, 2);

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task ZonesSurviveSavingTheMap()
    {
        var mapPath = new ResPath("/Maps/Test/ZoneSaveTest.yml");

        var zones = SEntMan.System<ZoneSystem>();
        var mapLoader = SEntMan.System<MapLoaderSystem>();
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var resManager = Server.ResolveDependency<IResourceManager>();

        await Server.WaitAssertion(() =>
        {
            resManager.UserData.CreateDir(mapPath.Directory);

            mapSystem.CreateMap(out var mapId);

            var grid = mapSystem.CreateGridEntity(mapId);
            mapSystem.SetTile(grid, new Vector2i(5, 0), new Tile(1));

            var comp = SEntMan.AddComponent<ZoneGridComponent>(grid.Owner);

            zones.PaintZoneRect((grid.Owner, comp), _cargoRect, "Cargo");
            zones.PaintZoneRect((grid.Owner, comp), _maintRect, "Maintenance");

            Assert.That(mapLoader.TrySaveMap(mapId, mapPath));
            mapSystem.DeleteMap(mapId);
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            Assert.That(mapLoader.TryLoadMap(mapPath, out var map, out var grids));

            var loaded = grids!.Single();

            Assert.That(SEntMan.TryGetComponent(loaded.Owner, out ZoneGridComponent? comp), Is.True,
                "The zone component did not come back with the grid.");

            var shapes = comp!.Shapes;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(shapes.Find(x => x.Zone == "Cargo")?.Rects, Is.Not.Null.And.Not.Empty);
                Assert.That(shapes.Find(x => x.Zone == "Maintenance")?.Rects,
                    Is.EqualTo([_maintRect]));

                Assert.That(zones.GetZoneId(loaded.Owner, new Vector2i(6, 0)),
                    Is.EqualTo(zones.GetZoneId("Maintenance")));
                Assert.That(zones.GetZoneId(loaded.Owner, new Vector2i(4, 0)),
                    Is.EqualTo(zones.GetZoneId("Cargo")));
                Assert.That(zones.GetZoneId(loaded.Owner, new Vector2i(20, 20)),
                    Is.EqualTo(SharedZoneSystem.NoZone));
            }

            mapSystem.DeleteMap(map!.Value.Comp.MapId);
        });
    }
}
