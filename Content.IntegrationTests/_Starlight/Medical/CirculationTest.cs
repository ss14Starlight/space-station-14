// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server.Body.Components;
using Content.Server._Starlight.Medical.Circulation;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Medical.Circulation;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._Starlight.Medical;

/// <summary>
/// Blood circulation: a healthy body is perfused and saturated, blood loss and a stopped heart starve the tissues
/// of oxygen, death stops the heart and revival restarts it, bodies without a heart are left alone.
/// </summary>
[TestFixture]
[TestOf(typeof(CirculationSystem))]
public sealed class CirculationTest : GameTest
{
    private const string Asphyxiation = "Asphyxiation";

    [Test]
    public async Task HealthyBody()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid human = default;
        await server.WaitPost(() => human = SpawnHuman(entMan, map.GridCoords));
        await Pair.RunSeconds(5);

        await server.WaitAssertion(() =>
        {
            var circulation = entMan.GetComponent<CirculationComponent>(human);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(circulation.HasCirculation);
                Assert.That(circulation.Perfusion, Is.EqualTo(1f).Within(0.01f));
                Assert.That(circulation.SpO2, Is.GreaterThan(0.9f));
                Assert.That(circulation.HeartRate, Is.EqualTo(75f).Within(1f));
                Assert.That(AsphyxiationDamage(entMan, human), Is.Zero, "a healthy body takes no hypoxia damage");
            }

            entMan.DeleteEntity(human);
        });
    }

    [Test]
    public async Task BloodLossStarvesTissues()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid human = default;
        await server.WaitPost(() =>
        {
            human = SpawnHuman(entMan, map.GridCoords);
            // No grace from the tissues' own oxygen: hypoxia shows right away.
            var circulation = entMan.GetComponent<CirculationComponent>(human);
            circulation.OxygenReserve = 0f;
            circulation.MaxOxygenReserve = 0f;
            var bloodstream = entMan.System<SharedBloodstreamSystem>();
            var comp = entMan.GetComponent<BloodstreamComponent>(human);
            // Lose about half the blood.
            bloodstream.TryModifyBloodLevel((human, comp), -comp.BloodReferenceSolution.Volume * 0.5f);
        });
        await Pair.RunSeconds(7);

        await server.WaitAssertion(() =>
        {
            var circulation = entMan.GetComponent<CirculationComponent>(human);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(circulation.Perfusion, Is.LessThan(0.5f));
                Assert.That(circulation.HeartRate, Is.GreaterThan(90f), "the heart races to make up for it");
                Assert.That(AsphyxiationDamage(entMan, human), Is.GreaterThan(Content.Shared.FixedPoint.FixedPoint2.Zero), "starved tissues take hypoxia damage");
            }

            entMan.DeleteEntity(human);
        });
    }

    [Test]
    public async Task HeartStopsWithDeath()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid human = default;
        await server.WaitPost(() => human = SpawnHuman(entMan, map.GridCoords));
        await Pair.RunSeconds(3);

        await server.WaitAssertion(() =>
        {
            var circulationSystem = entMan.System<CirculationSystem>();
            var heart = circulationSystem.GetHeart(human);
            Assert.That(heart, Is.Not.Null);

            // A stopped heart: no perfusion, the tissues starve.
            circulationSystem.SetRhythm(heart!.Value, HeartRhythm.Arrest);
        });
        await Pair.RunSeconds(5);

        await server.WaitAssertion(() =>
        {
            var circulation = entMan.GetComponent<CirculationComponent>(human);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(circulation.Perfusion, Is.Zero);
                Assert.That(circulation.HeartRate, Is.Zero);
                Assert.That(AsphyxiationDamage(entMan, human), Is.EqualTo(Content.Shared.FixedPoint.FixedPoint2.Zero), "the tissues live on their oxygen reserve for a while");
                Assert.That(circulation.OxygenReserve, Is.LessThan(circulation.MaxOxygenReserve));
            }

            circulation.OxygenReserve = 0f;
        });
        await Pair.RunSeconds(5);

        await server.WaitAssertion(() =>
        {
            var circulationSystem = entMan.System<CirculationSystem>();
            Assert.That(AsphyxiationDamage(entMan, human), Is.GreaterThan(Content.Shared.FixedPoint.FixedPoint2.Zero),
                "with the reserve spent, the tissues starve");

            // Death keeps it stopped, a revival starts it again.
            entMan.System<MobStateSystem>().ChangeMobState(human, Content.Shared.Mobs.MobState.Dead);
            Assert.That(circulationSystem.GetHeart(human)!.Value.Comp.Rhythm, Is.EqualTo(HeartRhythm.Arrest));
            entMan.EventBus.RaiseLocalEvent(human, new RejuvenateEvent());
            Assert.That(entMan.System<MobStateSystem>().IsAlive(human));
            Assert.That(circulationSystem.GetHeart(human)!.Value.Comp.Rhythm, Is.EqualTo(HeartRhythm.Normal));

            entMan.DeleteEntity(human);
        });
    }

    [Test]
    public async Task NoHeartNoCirculation()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid ipc = default;
        await server.WaitPost(() => ipc = entMan.SpawnEntity("MobIPC", map.GridCoords));
        await Pair.RunSeconds(5);

        await server.WaitAssertion(() =>
        {
            if (entMan.TryGetComponent<CirculationComponent>(ipc, out var circulation))
                Assert.That(circulation.HasCirculation, Is.False, "an IPC has no heart and no circulation");
            Assert.That(AsphyxiationDamage(entMan, ipc), Is.Zero);
            entMan.DeleteEntity(ipc);
        });
    }

    /// <summary>
    /// A human without lungs in the loop: the test map has no air, and breathing is not what is tested here.
    /// </summary>
    private static EntityUid SpawnHuman(IEntityManager entMan, Robust.Shared.Map.EntityCoordinates coords)
    {
        var human = entMan.SpawnEntity("MobHuman", coords);
        entMan.RemoveComponent<RespiratorComponent>(human);
        return human;
    }

    [Test]
    public async Task SuffocationLowersSaturation()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid human = default;
        await server.WaitPost(() =>
        {
            human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var circulation = entMan.GetComponent<CirculationComponent>(human);
            circulation.OxygenReserve = 0f;
            circulation.MaxOxygenReserve = 0f;
        });
        await Pair.RunSeconds(10);

        await server.WaitAssertion(() =>
        {
            // The test map is airless, so the lungs come up empty.
            var circulation = entMan.GetComponent<CirculationComponent>(human);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(circulation.SpO2, Is.LessThan(0.88f));
                Assert.That(circulation.OxygenDelivery, Is.LessThan(circulation.HypoxiaThreshold));
                Assert.That(AsphyxiationDamage(entMan, human), Is.GreaterThan(Content.Shared.FixedPoint.FixedPoint2.Zero));
            }

            entMan.DeleteEntity(human);
        });
    }

    private static Content.Shared.FixedPoint.FixedPoint2 AsphyxiationDamage(IEntityManager entMan, EntityUid uid)
        => entMan.System<DamageableSystem>().GetAllDamage(uid).DamageDict.TryGetValue(Asphyxiation, out var value)
            ? value
            : Content.Shared.FixedPoint.FixedPoint2.Zero;
}
