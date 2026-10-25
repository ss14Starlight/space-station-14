using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.IntegrationTests.Tests._Starlight.Weapons;

public sealed class PersonalEnergyDomeTest : GameTest
{
    [TestCase("EnergyDomeSmallRed")]
    [TestCase("EnergyDomeSmallPink")]
    [TestCase("EnergyDomeSmallCap")]
    [TestCase("EnergyDomeMediumBlue")]
    public async Task OnlyHolderProjectilesIgnoreDome(string prototype)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        await server.WaitAssertion(() =>
        {
            var holder = em.SpawnEntity(null, map.GridCoords);
            var stranger = em.SpawnEntity(null, map.GridCoords);
            var dome = em.SpawnEntity(prototype, new EntityCoordinates(holder, Vector2.Zero));
            var bullet = em.SpawnEntity("BulletEnergySMGLaser", map.GridCoords);
            var projectile = em.GetComponent<ProjectileComponent>(bullet);
            projectile.Armed = true;
            var projectiles = em.System<SharedProjectileSystem>();

            foreach (var shooter in new[] { holder, stranger, holder })
            {
                // Reflection replaces the shooter: the former holder's exemption must disappear.
                projectiles.SetShooter(bullet, projectile, shooter);
                var ev = new PreventCollideEvent(bullet, dome,
                    em.GetComponent<PhysicsComponent>(bullet), em.GetComponent<PhysicsComponent>(dome),
                    em.GetComponent<FixturesComponent>(bullet).Fixtures.Values.First(),
                    em.GetComponent<FixturesComponent>(dome).Fixtures.Values.First());
                em.EventBus.RaiseLocalEvent(bullet, ref ev);
                Assert.That(ev.Cancelled, Is.EqualTo(shooter == holder));
            }

            em.DeleteEntity(bullet);
            em.DeleteEntity(dome);
            em.DeleteEntity(holder);
            em.DeleteEntity(stranger);
        });
    }

    [TestCase("EnergyDomeSmallRed")]
    [TestCase("EnergyDomeMediumBlue")]
    public async Task HitscanInsideDomeOnlyExemptsHolder(string prototype)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        EntityUid holder = default, stranger = default, dome = default, target = default, shot = default;
        await server.WaitPost(() =>
        {
            holder = em.SpawnEntity(null, map.GridCoords);
            stranger = em.SpawnEntity(null, map.GridCoords);
            dome = em.SpawnEntity(prototype, new EntityCoordinates(holder, Vector2.Zero));
            target = em.SpawnEntity("WallSolid", map.GridCoords.Offset(new Vector2(4, 0)));
            shot = em.SpawnEntity("PracticeBulletTrace", map.GridCoords);
        });
        await RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var raycast = em.System<HitscanBasicRaycastSystem>();
            var hitscan = new Entity<HitscanBasicRaycastComponent>(shot, em.GetComponent<HitscanBasicRaycastComponent>(shot));
            var outgoing = raycast.PredictTrace(hitscan, holder, map.GridCoords, Vector2.UnitX, 4, target, 1);
            Assert.That(outgoing[0].ImpactedEnt, Is.EqualTo(em.GetNetEntity(target)));
            var unsynchronized = raycast.PredictTrace(hitscan, stranger, map.GridCoords, Vector2.UnitX, 4, target, 1);
            Assert.That(unsynchronized[0].ImpactedEnt, Is.EqualTo(em.GetNetEntity(dome)));
            var incoming = raycast.PredictTrace(hitscan, stranger,
                map.GridCoords.Offset(new Vector2(-4, 0)), Vector2.UnitX, 4, holder, 1);
            Assert.That(incoming[0].ImpactedEnt, Is.EqualTo(em.GetNetEntity(dome)));
            foreach (var uid in new[] { shot, target, dome, holder, stranger })
                em.DeleteEntity(uid);
        });
    }

    [Test]
    public async Task PenetrationHitsOverlappingDomesOnceEach()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        EntityUid holder = default, stranger = default, first = default, second = default, shot = default;
        await server.WaitPost(() =>
        {
            holder = em.SpawnEntity(null, map.GridCoords);
            stranger = em.SpawnEntity(null, map.GridCoords);
            first = em.SpawnEntity("EnergyDomeSmallRed", new EntityCoordinates(holder, Vector2.Zero));
            second = em.SpawnEntity("EnergyDomeSmallRed", new EntityCoordinates(holder, Vector2.Zero));
            shot = em.SpawnEntity("BulletRifleTraceAP", map.GridCoords);
            var pierce = em.GetComponent<HitscanPierceComponent>(shot);
            pierce.Chance = 1;
            pierce.Deviation = 0;
        });
        await RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var hitscan = new Entity<HitscanBasicRaycastComponent>(shot, em.GetComponent<HitscanBasicRaycastComponent>(shot));
            var traces = em.System<HitscanBasicRaycastSystem>().PredictTrace(hitscan, stranger,
                map.GridCoords.Offset(new Vector2(-4, 0)), Vector2.UnitX, 4, holder, 1);
            Assert.That(traces.Count(trace => trace.ImpactedEnt == em.GetNetEntity(first)), Is.EqualTo(1));
            Assert.That(traces.Count(trace => trace.ImpactedEnt == em.GetNetEntity(second)), Is.EqualTo(1));
            foreach (var uid in new[] { shot, first, second, holder, stranger })
                em.DeleteEntity(uid);
        });
    }
}
