using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.IntegrationTests.Tests._Starlight.Weapons;

/// <summary>
/// Checks that personal shields catch shots from either side and allow normal penetration.
/// </summary>
public sealed class PersonalEnergyDomeTest : GameTest
{
    [TestCase("EnergyDomeSmallRed")]
    [TestCase("EnergyDomeSmallPink")]
    [TestCase("EnergyDomeSmallCap")]
    [TestCase("EnergyDomeMediumBlue")]
    public async Task ProjectilesHitDomeFromInside(string prototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var owner = SEntMan.SpawnEntity(null, map.GridCoords);
            var stranger = SEntMan.SpawnEntity(null, map.GridCoords);
            var dome = SEntMan.SpawnEntity(prototype, new EntityCoordinates(owner, Vector2.Zero));
            var bullet = SEntMan.SpawnEntity("BulletEnergySMGLaser", map.GridCoords);
            var projectile = SEntMan.GetComponent<ProjectileComponent>(bullet);
            var projectiles = SEntMan.System<SharedProjectileSystem>();

            foreach (var armed in new[] { false, true })
            {
                projectile.Armed = armed;
                foreach (var shooter in new[] { owner, stranger })
                {
                    projectiles.SetShooter(bullet, projectile, shooter);
                    var ev = new PreventCollideEvent(bullet, dome,
                        SEntMan.GetComponent<PhysicsComponent>(bullet), SEntMan.GetComponent<PhysicsComponent>(dome),
                        SEntMan.GetComponent<FixturesComponent>(bullet).Fixtures.Values.First(),
                        SEntMan.GetComponent<FixturesComponent>(dome).Fixtures.Values.First());
                    SEntMan.EventBus.RaiseLocalEvent(bullet, ref ev);
                    Assert.That(ev.Cancelled, Is.False,
                        armed ? "An armed projectile skipped the shield." : "An unarmed projectile skipped the shield.");
                }
            }

            SEntMan.DeleteEntity(bullet);
            SEntMan.DeleteEntity(dome);
            SEntMan.DeleteEntity(owner);
            SEntMan.DeleteEntity(stranger);
        });
    }

    [TestCase("EnergyDomeSmallRed")]
    [TestCase("EnergyDomeMediumBlue")]
    public async Task HitscanHitsDomeFromBothSides(string prototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid owner = default;
        EntityUid stranger = default;
        EntityUid dome = default;
        EntityUid target = default;
        EntityUid shot = default;
        EntityUid piercingShot = default;

        await Server.WaitPost(() =>
        {
            owner = SEntMan.SpawnEntity(null, map.GridCoords);
            stranger = SEntMan.SpawnEntity(null, map.GridCoords);
            dome = SEntMan.SpawnEntity(prototype, new EntityCoordinates(owner, Vector2.Zero));
            target = SEntMan.SpawnEntity("WallSolid", map.GridCoords.Offset(new Vector2(4, 0)));
            shot = SEntMan.SpawnEntity("PracticeBulletTrace", map.GridCoords);
            piercingShot = SEntMan.SpawnEntity("BulletRifleTraceAP", map.GridCoords);
            var pierce = SEntMan.GetComponent<HitscanPierceComponent>(piercingShot);
            pierce.Chance = 1;
            pierce.Deviation = 0;
        });
        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var raycast = SEntMan.System<HitscanBasicRaycastSystem>();
            var hitscan = new Entity<HitscanBasicRaycastComponent>(shot, SEntMan.GetComponent<HitscanBasicRaycastComponent>(shot));
            var outgoing = raycast.PredictTrace(hitscan, owner, map.GridCoords, Vector2.UnitX, 4, target, 1);
            Assert.That(outgoing, Is.Not.Empty, "The owner's shot from inside the shield produced no trace.");
            Assert.That(outgoing[0].ImpactedEnt, Is.EqualTo(SEntMan.GetNetEntity(dome)),
                "The owner's shot from inside missed the shield.");

            var piercingHitscan = new Entity<HitscanBasicRaycastComponent>(piercingShot,
                SEntMan.GetComponent<HitscanBasicRaycastComponent>(piercingShot));
            var piercing = raycast.PredictTrace(piercingHitscan, owner, map.GridCoords, Vector2.UnitX, 4, target, 1);
            Assert.That(piercing.Count(trace => trace.ImpactedEnt == SEntMan.GetNetEntity(dome)), Is.EqualTo(1),
                "The owner's piercing shot missed the shield.");
            Assert.That(piercing.Any(trace => trace.ImpactedEnt == SEntMan.GetNetEntity(target)), Is.True,
                "The owner's piercing shot did not reach the target.");

            var inside = raycast.PredictTrace(hitscan, stranger, map.GridCoords, Vector2.UnitX, 4, target, 1);
            Assert.That(inside, Is.Not.Empty, "Another shooter's shot from inside the shield produced no trace.");
            Assert.That(inside[0].ImpactedEnt, Is.EqualTo(SEntMan.GetNetEntity(dome)),
                "Another shooter inside the shield shot through it.");

            var incoming = raycast.PredictTrace(hitscan, stranger,
                map.GridCoords.Offset(new Vector2(-4, 0)), Vector2.UnitX, 4, owner, 1);
            Assert.That(incoming, Is.Not.Empty, "The shot fired at the shield from outside produced no trace.");
            Assert.That(incoming[0].ImpactedEnt, Is.EqualTo(SEntMan.GetNetEntity(dome)),
                "The incoming shot missed the shield.");

            foreach (var uid in new[] { shot, piercingShot, target, dome, owner, stranger })
                SEntMan.DeleteEntity(uid);
        });
    }

    [TestCase(false, true, TestName = "PredictionHitsBothOverlappingShields")]
    [TestCase(true, true, TestName = "PredictionHitsOwnShieldAfterPenetration")]
    [TestCase(false, false, TestName = "LiveHitscanHitsBothOverlappingShields")]
    [TestCase(true, false, TestName = "LiveHitscanHitsOwnShieldAfterPenetration")]
    public async Task PenetrationHitsOverlappingDomesOnceEach(bool ownsSecondDome, bool predict)
    {
        var map = await Pair.CreateTestMap();
        EntityUid owner = default;
        EntityUid stranger = default;
        EntityUid first = default;
        EntityUid second = default;
        EntityUid shot = default;

        await Server.WaitPost(() =>
        {
            owner = SEntMan.SpawnEntity(null, map.GridCoords);
            stranger = SEntMan.SpawnEntity(null, map.GridCoords);
            first = SEntMan.SpawnEntity("EnergyDomeSmallRed", new EntityCoordinates(owner, Vector2.Zero));
            var secondOwner = ownsSecondDome ? stranger : owner;
            second = SEntMan.SpawnEntity("EnergyDomeSmallRed", new EntityCoordinates(secondOwner, Vector2.Zero));
            shot = SEntMan.SpawnEntity("BulletRifleTraceAP", map.GridCoords);

            // Always penetrate in a straight line so these tests do not depend on random rolls.
            var pierce = SEntMan.GetComponent<HitscanPierceComponent>(shot);
            pierce.Chance = 1;
            pierce.Deviation = 0;
        });
        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var hitscan = new Entity<HitscanBasicRaycastComponent>(shot, SEntMan.GetComponent<HitscanBasicRaycastComponent>(shot));
            var from = map.GridCoords.Offset(ownsSecondDome ? Vector2.Zero : new Vector2(-4, 0));
            var firing = new HitscanTraceEvent
            {
                FromCoordinates = from,
                ToCoordinates = from.Offset(new Vector2(4, 0)),
                ShotDirection = Vector2.UnitX,
                Gun = stranger,
                Shooter = stranger,
                Target = owner,
                PredictionSeed = 1,
            };
            if (!predict)
                SEntMan.EventBus.RaiseLocalEvent(shot, ref firing);

            var traces = predict
                ? SEntMan.System<HitscanBasicRaycastSystem>().PredictTrace(hitscan, stranger, from, Vector2.UnitX, 4, owner, 1)
                : firing.OutputTrace;

            Assert.That(traces, Is.Not.Null, predict
                ? "The predicted shot through overlapping shields produced no trace."
                : "The fired shot through overlapping shields produced no trace.");
            Assert.That(traces.Count(trace => trace.ImpactedEnt == SEntMan.GetNetEntity(first)), Is.EqualTo(1),
                "The first shield should be hit once.");
            Assert.That(traces.Count(trace => trace.ImpactedEnt == SEntMan.GetNetEntity(second)), Is.EqualTo(1),
                "The second shield should be hit once.");

            foreach (var uid in new[] { shot, first, second, owner, stranger })
                SEntMan.DeleteEntity(uid);
        });
    }
}
