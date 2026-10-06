using System.Linq;
using Content.Client._Starlight.Zones;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Zones;
using Content.Shared._Starlight.Zones;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Starlight.Zones;

[TestFixture]
[TestOf(typeof(ZonePlacementSystem))]
public sealed class ZoneSyncTest : GameTest
{
    /// <summary>
    /// Tests that when a zone is painted on the server, the client receives the shape data correctly.
    /// </summary>
    [Test]
    public async Task ShapesReachTheClient()
    {
        var session = ServerSession;
        Assert.That(session, Is.Not.Null, "This test needs a connected player.");

        var zones = SEntMan.System<ZoneSystem>();
        var testMap = await Pair.CreateTestMap();

        NetEntity netGrid = default;

        await Server.WaitPost(() =>
        {
            var grid = SEntMan.System<SharedMapSystem>().CreateGridEntity(testMap.MapId);
            var comp = SEntMan.AddComponent<ZoneGridComponent>(grid.Owner);
            netGrid = SEntMan.GetNetEntity(grid.Owner);

            zones.PaintZoneRect((grid.Owner, comp), new Box2i(3, -5, 11, 7), "Cargo");
            zones.SendZoneShapes((grid.Owner, comp), session!);
        });

        await RunTicksSync(10);

        var placement = CEntMan.System<ZonePlacementSystem>();
        var known = placement.GetKnownShapes(netGrid);

        Assert.That(known, Is.Not.Null, "The client never received the shapes.");

        var set = known!.SingleOrDefault(x => x.Zone == "Cargo");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(set, Is.Not.Null, "The zone came across without its prototype.");
            Assert.That(set!.Rects, Has.Count.EqualTo(1));
            Assert.That(set.Rects[0], Is.EqualTo(new Box2i(3, -5, 11, 7)),
                "The rectangle did not survive serialisation intact.");
        }
    }
}
