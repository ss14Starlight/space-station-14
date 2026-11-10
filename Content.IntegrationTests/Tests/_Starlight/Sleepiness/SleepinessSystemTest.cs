#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Content.Shared._Starlight.Sleepiness.Components;
using Content.Shared._Starlight.Sleepiness.Systems;
using Content.Shared._Starlight.Tiredness.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Sleepiness;

[TestFixture]
public sealed class SleepinessSystemTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

    [Test]
    public async Task LightSleepDoesNotApplyToOrdinarySleep()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        EntityUid felionoid = default;

        await server.WaitPost(() => felionoid = entityManager.Spawn("MobFelionoid", MapCoordinates.Nullspace));

        try
        {
            await pair.RunSeconds(1);

            await server.WaitPost(() =>
            {
                var sleeping = server.System<SleepingSystem>();
                var blindable = entityManager.GetComponent<BlindableComponent>(felionoid);

                entityManager.RemoveComponent<LightSleepComponent>(felionoid);
                Assert.That(sleeping.TrySleeping(felionoid), Is.True);
                Assert.That(blindable.IsBlind, Is.True);

                entityManager.AddComponent<LightSleepComponent>(felionoid);
                Assert.That(blindable.IsBlind, Is.True);

                entityManager.RemoveComponent<LightSleepComponent>(felionoid);
                Assert.That(blindable.IsBlind, Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() => entityManager.DeleteEntity(felionoid));
            await pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task LightSleepDoesNotOverrideSleepBlindnessDuringPlayerRequestedSleep()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        EntityUid felionoid = default;

        await server.WaitPost(() => felionoid = entityManager.Spawn("MobFelionoid", MapCoordinates.Nullspace));

        try
        {
            await pair.RunSeconds(1);

            await server.WaitPost(() =>
            {
                entityManager.AddComponent<PlayerRequestedSleepComponent>(felionoid);

                var sleeping = server.System<SleepingSystem>();
                var blindable = entityManager.GetComponent<BlindableComponent>(felionoid);
                Assert.That(sleeping.TrySleeping(felionoid), Is.True);
                Assert.That(server.System<LightSleepSystem>().IsLightSleepActive(felionoid), Is.True);
                Assert.That(blindable.IsBlind, Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() => entityManager.DeleteEntity(felionoid));
            await pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task NocturineCanInduceSleepWhenTirednessSleepIsDisabled()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        EntityUid felionoid = default;

        await server.WaitPost(() =>
        {
            felionoid = entityManager.Spawn("MobFelionoid", MapCoordinates.Nullspace);
            var tiredness = entityManager.GetComponent<TirednessComponent>(felionoid);
            tiredness.CanInduceSleep = false;
            entityManager.Dirty(felionoid, tiredness);
        });

        try
        {
            await pair.RunSeconds(1);

            var nocturineAdded = false;
            await server.WaitPost(() =>
            {
                var bloodstream = entityManager.GetComponent<BloodstreamComponent>(felionoid);
                var solutions = server.System<SharedSolutionContainerSystem>();
                if (!solutions.ResolveSolution(
                        felionoid,
                        bloodstream.BloodSolutionName,
                        ref bloodstream.BloodSolution,
                        out _) ||
                    bloodstream.BloodSolution is not { } bloodSolution)
                    return;

                solutions.TryAddReagent(
                    bloodSolution,
                    new ReagentQuantity("Nocturine", FixedPoint2.New(100)),
                    out var acceptedQuantity);
                nocturineAdded = acceptedQuantity >= FixedPoint2.New(4);
            });
            Assert.That(nocturineAdded, Is.True);

            await pair.RunSeconds(15);
            await server.WaitAssertion(() =>
                Assert.That(entityManager.HasComponent<SleepingComponent>(felionoid), Is.True));
        }
        finally
        {
            await server.WaitPost(() => entityManager.DeleteEntity(felionoid));
            await pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task RemovingSleepinessOnlyWakesWhenRequested()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        EntityUid felionoid = default;

        await server.WaitPost(() =>
        {
            felionoid = entityManager.Spawn("MobFelionoid", MapCoordinates.Nullspace);
            var tiredness = entityManager.GetComponent<TirednessComponent>(felionoid);
            tiredness.CanInduceSleep = true;
            entityManager.Dirty(felionoid, tiredness);
        });

        try
        {
            await pair.RunSeconds(1);

            var sleepinessAdded = false;
            await server.WaitPost(() =>
            {
                var statusEffects = server.System<StatusEffectsSystem>();
                sleepinessAdded = statusEffects.TryAddStatusEffectDuration(
                    felionoid,
                    "StatusEffectSleepiness",
                    TimeSpan.FromSeconds(120));
            });
            Assert.That(sleepinessAdded, Is.True);

            await pair.RunSeconds(1);
            await server.WaitAssertion(() =>
                Assert.That(entityManager.HasComponent<SleepingComponent>(felionoid), Is.True));

            var firstSleepinessRemoved = false;
            await server.WaitPost(() =>
            {
                var statusEffects = server.System<StatusEffectsSystem>();
                firstSleepinessRemoved = statusEffects.TryRemoveStatusEffect(felionoid, "StatusEffectSleepiness");
            });
            Assert.That(firstSleepinessRemoved, Is.True);

            await pair.RunSeconds(1);
            await server.WaitAssertion(() =>
                Assert.That(entityManager.HasComponent<SleepingComponent>(felionoid), Is.True));

            var wakeSleepinessAdded = false;
            var wakeSleepinessRemoved = false;
            await server.WaitPost(() =>
            {
                var statusEffects = server.System<StatusEffectsSystem>();
                wakeSleepinessAdded = statusEffects.TryAddStatusEffectDuration(
                    felionoid,
                    "StatusEffectSleepiness",
                    out var sleepinessEffect,
                    TimeSpan.FromSeconds(10));

                if (!wakeSleepinessAdded || sleepinessEffect is not { } sleepinessUid)
                    return;

                var sleepiness = entityManager.GetComponent<SleepinessStatusEffectComponent>(sleepinessUid);
                sleepiness.WakeRequested = true;
                entityManager.Dirty(sleepinessUid, sleepiness);
                wakeSleepinessRemoved = statusEffects.TryRemoveStatusEffect(felionoid, "StatusEffectSleepiness");
            });
            Assert.That(wakeSleepinessAdded, Is.True);
            Assert.That(wakeSleepinessRemoved, Is.True);

            await pair.RunSeconds(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(entityManager.HasComponent<SleepingComponent>(felionoid), Is.False);
                Assert.That(entityManager.HasComponent<SleepinessInducedSleepComponent>(felionoid), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() => entityManager.DeleteEntity(felionoid));
            await pair.RunUntilSynced();
        }
    }
}
