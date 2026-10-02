using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Zones;
using Content.Shared._Starlight.Zones;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Content.Shared.Random.Rules;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Zones;

[TestFixture]
[TestOf(typeof(SharedZoneTrackerSystem))]
public sealed class ZoneTrackerTest : GameTest
{
    private const int Width = 8;
    private const int Height = 4;
    private const int CargoRight = 4;
    private const string MaintenanceZone = "InMaintenanceZone";

    /// <summary>
    /// Tests that the zone tracker component correctly follows an entity as it moves between zones, including leaving a zone and entering another.
    /// </summary>
    [Test]
    public async Task TrackerFollowsTheEntityBetweenZones()
    {
        var zones = SEntMan.System<ZoneSystem>();
        var xforms = SEntMan.System<SharedTransformSystem>();
        var tileDefs = Server.ResolveDependency<ITileDefinitionManager>();

        var testMap = await Pair.CreateTestMap();

        EntityUid grid = default;
        EntityUid dummy = default;

        await Server.WaitPost(() =>
        {
            var gridEnt = SEntMan.System<SharedMapSystem>().CreateGridEntity(testMap.MapId);
            grid = gridEnt.Owner;

            var plating = new Tile(tileDefs["Plating"].TileId);

            for (var x = 0; x < CargoRight; x++)
            for (var y = 0; y < Height; y++)
                SEntMan.System<SharedMapSystem>().SetTile(gridEnt, gridEnt, new Vector2i(x, y), plating);

            var comp = SEntMan.AddComponent<ZoneGridComponent>(grid);
            zones.PaintZoneRect((grid, comp), new Box2i(0, 0, CargoRight, Height), "Cargo");
            zones.PaintZoneRect((grid, comp), new Box2i(CargoRight, 0, Width, Height), "Maintenance");
            zones.FullRebuild((grid, comp));

            dummy = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, 1.5f, 1.5f));
            SEntMan.AddComponent<ZoneTrackerComponent>(dummy);
        });

        await RunTicksSync(30);

        Assert.That(Zone(dummy)?.Id, Is.EqualTo("Cargo"), "The tracker never picked up the zone.");

        await Server.WaitPost(() => xforms.SetCoordinates(dummy, new EntityCoordinates(grid, CargoRight + 0.5f, 1.5f)));

        await RunTicksSync(30);

        Assert.That(Zone(dummy)?.Id, Is.EqualTo("Maintenance"), "The tracker did not update when entering another zone.");

        await Server.WaitPost(() => xforms.SetCoordinates(dummy, new EntityCoordinates(grid, Width + 0.5f, 1.5f)));

        await RunTicksSync(30);

        Assert.That(Zone(dummy), Is.Null, "Walking off the station should have cleared the zone.");

        return;

        ProtoId<ZonePrototype>? Zone(EntityUid uid)
            => SEntMan.GetComponent<ZoneTrackerComponent>(uid).Zone;
    }

    /// <summary>
    /// Tests that rules can correctly query which zone an entity is in, and that the result changes when the entity is moved into a different zone.
    /// </summary>
    [Test]
    public async Task RulesCanAskWhichZoneSomebodyIsIn()
    {
        var rules = SEntMan.System<RulesSystem>();
        var maintenance = SProtoMan.Index<RulesPrototype>(MaintenanceZone);

        var dummy = EntityUid.Invalid;

        await Server.WaitPost(() =>
        {
            var testMap = SEntMan.System<SharedMapSystem>();
            testMap.CreateMap(out var mapId);

            dummy = SEntMan.SpawnEntity(null, new MapCoordinates(Vector2.Zero, mapId));
            SEntMan.AddComponent<ZoneTrackerComponent>(dummy);
        });

        Assert.That(rules.IsTrue(dummy, maintenance), Is.False, "Nowhere in particular is not maintenance.");

        await Server.WaitPost(() =>
        {
            var tracker = SEntMan.System<SharedZoneTrackerSystem>();
            tracker.SetZone((dummy, SEntMan.GetComponent<ZoneTrackerComponent>(dummy)), "Maintenance", default);
        });

        Assert.That(rules.IsTrue(dummy, maintenance), Is.True);
    }
}
