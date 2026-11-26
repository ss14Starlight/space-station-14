using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Bed.Sleep;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.IntegrationTests.Tests._Starlight.Nutrition;

public sealed class HungerThirstSleepTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [Test]
    public async Task SleepingReducesHungerAndThirstDrain()
    {
        await AddAtmosphere();

        var pair = Pair;
        var server = pair.Server;
        var hungerComponent = Comp<HungerComponent>(Player);
        var thirstComponent = Comp<ThirstComponent>(Player);
        var hungerSystem = SEntMan.System<HungerSystem>();
        var thirstSystem = SEntMan.System<ThirstSystem>();
        var sleepingSystem = SEntMan.System<SleepingSystem>();
        const float InitialHunger = 150f;
        const float InitialThirst = 500f;

        hungerSystem.SetHunger(SPlayer, InitialHunger, hungerComponent);
        thirstSystem.SetThirst(SPlayer, thirstComponent, InitialThirst);
        hungerSystem.AddHungerDrain(SPlayer, 1f, null, hungerComponent);
        thirstSystem.AddThirstDrain(SPlayer, 1f, null, thirstComponent);

        try
        {
            var awakeHungerStart = hungerSystem.GetHunger(hungerComponent);
            var awakeThirstStart = thirstComponent.CurrentThirst;
            await RunSeconds(6);

            var awakeHungerDrain = awakeHungerStart - hungerSystem.GetHunger(hungerComponent);
            var awakeThirstDrain = awakeThirstStart - thirstComponent.CurrentThirst;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(awakeHungerDrain, Is.GreaterThan(0f));
                Assert.That(awakeThirstDrain, Is.GreaterThan(0f));
            }

            await server.WaitAssertion(() =>
            {
                Assert.That(sleepingSystem.TrySleeping(SPlayer), Is.True);
                hungerSystem.SetHunger(SPlayer, InitialHunger, hungerComponent);
                thirstSystem.SetThirst(SPlayer, thirstComponent, InitialThirst);
            });

            var sleepingHungerStart = hungerSystem.GetHunger(hungerComponent);
            var sleepingThirstStart = thirstComponent.CurrentThirst;
            await RunSeconds(6);

            var sleepingHungerDrain = sleepingHungerStart - hungerSystem.GetHunger(hungerComponent);
            var sleepingThirstDrain = sleepingThirstStart - thirstComponent.CurrentThirst;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(sleepingHungerDrain,
                    Is.EqualTo(awakeHungerDrain / 3f).Within(awakeHungerDrain * 0.1f));
                Assert.That(sleepingThirstDrain,
                    Is.EqualTo(awakeThirstDrain / 3f).Within(awakeThirstDrain * 0.1f));
            }
        }
        finally
        {
            await server.WaitPost(() =>
            {
                sleepingSystem.TryWaking(SPlayer, force: true);
                hungerSystem.RemoveHungerDrain(SPlayer, null, hungerComponent);
                thirstSystem.RemoveThirstDrain(SPlayer, null, thirstComponent);
            });
        }
    }
}
