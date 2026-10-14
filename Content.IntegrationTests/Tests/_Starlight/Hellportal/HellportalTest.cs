using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._Moffstation.Hellportal;
using Content.Server._Moffstation.Hellportal.Components;
using Robust.Server.Player;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Hellportal;

[TestOf(typeof(HellportalSystem))]
public sealed class HellportalTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
    -   type: entity
        id: HellportalQuotaTestPortal
        components:
        -   type: Transform
            anchored: true
        -   type: Physics
            bodyType: Static
        -   type: Fixtures
        -   type: Hellportal
            maxSpawns: 2
            spawnCooldown: 86400
            nextSpawn: 86400
            basicSpawnTable: !type:EntSelector
                id: HellportalQuotaTestMob
                amount: !type:ConstantNumberSelector
                    value: 3

    -   type: entity
        id: HellportalQuotaTestMob
        components:
        -   type: HellportalMob
    """;

    [Test]
    public async Task PortalsHaveIndependentLimitsAndWavesCannotExceedThem()
    {
        var map = await Pair.CreateTestMap();
        var system = SEntMan.System<HellportalSystem>();

        await Server.WaitAssertion(() =>
        {
            var coords = map.GridCoords;
            for (var i = 0; i < 3; i++)
                SSpawnAtPosition("HellportalQuotaTestMob", coords);

            var first = SSpawnAtPosition("HellportalQuotaTestPortal", coords);
            var firstComp = SComp<HellportalComponent>(first);
            firstComp.NextSpawn = TimeSpan.Zero;
            system.Update(0f);
            Assert.That(GetOwnedMobs(first), Has.Count.EqualTo(2),
                "Unowned mobs must not use this portal's quota, and a three-mob wave must stop at two.");

            var second = SSpawnAtPosition("HellportalQuotaTestPortal", coords);
            var secondComp = SComp<HellportalComponent>(second);

            try
            {
                firstComp.NextSpawn = TimeSpan.Zero;
                secondComp.NextSpawn = TimeSpan.Zero;
                system.Update(0f);
                Assert.That(GetOwnedMobs(first), Has.Count.EqualTo(2));
                Assert.That(GetOwnedMobs(second), Has.Count.EqualTo(2),
                    "A full first portal must not stop a newly created second portal from spawning.");

                SEntMan.DeleteEntity(GetOwnedMobs(first)[0]);
                firstComp.NextSpawn = TimeSpan.Zero;
                secondComp.NextSpawn = TimeSpan.Zero;
                system.Update(0f);
                Assert.That(GetOwnedMobs(first), Has.Count.EqualTo(2),
                    "Deleting a mob must free only its source portal's quota.");
                Assert.That(GetOwnedMobs(second), Has.Count.EqualTo(2));
            }
            finally
            {
                foreach (var mob in GetOwnedMobs(first))
                    SEntMan.DeleteEntity(mob);
                foreach (var mob in GetOwnedMobs(second))
                    SEntMan.DeleteEntity(mob);
            }
        });
    }

    [Test]
    public async Task PopulationScalingWorksBelowTheFixedLimit()
    {
        var map = await Pair.CreateTestMap();
        var system = SEntMan.System<HellportalSystem>();
        var players = Server.ResolveDependency<IPlayerManager>();

        await Server.WaitAssertion(() =>
        {
            Assert.That(players.PlayerCount, Is.EqualTo(1), "This fixture connects one test player.");
            var portal = SSpawnAtPosition("HellportalQuotaTestPortal", map.GridCoords);
            var comp = SComp<HellportalComponent>(portal);
            comp.MobsPerPlayer = 0.75f;

            try
            {
                comp.NextSpawn = TimeSpan.Zero;
                system.Update(0f);
                Assert.That(GetOwnedMobs(portal), Has.Count.EqualTo(1),
                    "Low population must round up to one mob instead of using the fixed limit of two.");

                comp.NextSpawn = TimeSpan.Zero;
                system.Update(0f);
                Assert.That(GetOwnedMobs(portal), Has.Count.EqualTo(1));
            }
            finally
            {
                foreach (var mob in GetOwnedMobs(portal))
                    SEntMan.DeleteEntity(mob);
            }
        });
    }

    private List<EntityUid> GetOwnedMobs(EntityUid portal)
    {
        var mobs = new List<EntityUid>();
        var query = SEntMan.AllEntityQueryEnumerator<HellportalMobComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SourcePortal == portal)
                mobs.Add(uid);
        }

        return mobs;
    }
}
