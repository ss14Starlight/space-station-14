using Content.IntegrationTests.Fixtures;
using Content.Shared.Bed.Sleep;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Starlight.Nutrition;

[TestFixture]
public sealed class SleepingNutritionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

    [Test]
    public async Task SleepAndWakeScaleNutritionDecay()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid human = default;
        var hungerSystem = server.System<HungerSystem>();
        var thirstSystem = server.System<ThirstSystem>();
        var hungerDecayRate = 0f;
        var thirstDecayRate = 0f;
        var sleepStartHunger = 0f;
        var sleepStartThirst = 0f;
        var sleepStartTime = TimeSpan.Zero;
        var wakeStartHunger = 0f;
        var wakeStartThirst = 0f;
        var wakeStartTime = TimeSpan.Zero;

        try
        {
            await server.WaitPost(() => human = entityManager.Spawn("MobHuman"));
            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var hunger = entityManager.GetComponent<HungerComponent>(human);
                var thirst = entityManager.GetComponent<ThirstComponent>(human);
                hungerSystem.SetHunger(human, hunger.Thresholds[HungerThreshold.Okay], hunger);
                thirstSystem.SetThirst(human, thirst, 400f);

                hungerDecayRate = hunger.ActualDecayRate;
                thirstDecayRate = thirst.ActualDecayRate;
                Assert.That(hungerDecayRate, Is.GreaterThan(0f));
                Assert.That(thirstDecayRate, Is.GreaterThan(0f));
                hungerSystem.AddHungerDrain(human, 1f, null, hunger);
                Assert.That(server.System<SleepingSystem>().TrySleeping(human), Is.True);
                Assert.That(hunger.ActualDecayRate, Is.EqualTo(hungerDecayRate));
                Assert.That(thirst.ActualDecayRate, Is.EqualTo(thirstDecayRate));
                Assert.That(thirst.ThirstDrains, Is.Empty);

                sleepStartHunger = hungerSystem.GetHunger(hunger);
                sleepStartThirst = thirst.CurrentThirst;
                sleepStartTime = timing.CurTime;
            });

            await pair.RunSeconds(30f);

            await server.WaitAssertion(() =>
            {
                var hunger = entityManager.GetComponent<HungerComponent>(human);
                var thirst = entityManager.GetComponent<ThirstComponent>(human);
                var elapsed = timing.CurTime - sleepStartTime;
                var sleepingHunger = hungerSystem.GetHunger(hunger);
                Assert.That(sleepStartHunger - sleepingHunger,
                    Is.EqualTo(hungerDecayRate * (float) elapsed.TotalSeconds * 2f / 3f)
                        .Within(hungerDecayRate / 3f + 0.001f));

                var expectedThirstDrain = thirstDecayRate * (float) elapsed.TotalSeconds /
                    (float) thirst.UpdateRate.TotalSeconds / 3f;
                Assert.That(sleepStartThirst - thirst.CurrentThirst,
                    Is.EqualTo(expectedThirstDrain).Within(thirstDecayRate / 3f + 0.001f));

                hungerSystem.RemoveHungerDrain(human, null, hunger);
                entityManager.RemoveComponent<SleepingComponent>(human);
                Assert.That(hunger.ActualDecayRate, Is.EqualTo(hungerDecayRate));
                Assert.That(hungerSystem.GetHunger(hunger), Is.EqualTo(sleepingHunger).Within(0.001f));
                Assert.That(thirst.ActualDecayRate, Is.EqualTo(thirstDecayRate));
                Assert.That(thirst.ThirstDrains, Is.Empty);

                wakeStartHunger = hungerSystem.GetHunger(hunger);
                wakeStartThirst = thirst.CurrentThirst;
                wakeStartTime = timing.CurTime;
            });

            await pair.RunSeconds(30f);

            await server.WaitAssertion(() =>
            {
                var hunger = entityManager.GetComponent<HungerComponent>(human);
                var thirst = entityManager.GetComponent<ThirstComponent>(human);
                var elapsed = timing.CurTime - wakeStartTime;
                Assert.That(wakeStartHunger - hungerSystem.GetHunger(hunger),
                    Is.EqualTo(hungerDecayRate * (float) elapsed.TotalSeconds).Within(0.001f));

                var expectedThirstDrain = thirstDecayRate * (float) elapsed.TotalSeconds /
                    (float) thirst.UpdateRate.TotalSeconds;
                Assert.That(wakeStartThirst - thirst.CurrentThirst,
                    Is.EqualTo(expectedThirstDrain).Within(thirstDecayRate + 0.001f));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (human.IsValid())
                    entityManager.DeleteEntity(human);
            });
            await pair.RunUntilSynced();
        }
    }
}
