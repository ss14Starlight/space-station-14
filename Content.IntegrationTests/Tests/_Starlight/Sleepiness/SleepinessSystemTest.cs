#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared._Starlight.EntityEffects.Effects.StatusEffects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Sleepiness;

[TestFixture]
public sealed class SleepinessSystemTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

    [Test]
    public void SleepinessInducingReagentsDoNotConfigureMovementSpeedModifier()
    {
        var prototypeManager = Pair.Server.ResolveDependency<IPrototypeManager>();
        var sleepinessInducingReagents = prototypeManager.EnumeratePrototypes<ReagentPrototype>()
            .Where(reagent => reagent.Metabolisms?.Metabolisms.Values
                .SelectMany(metabolism => metabolism.Effects)
                .Any(effect => effect is ModifySleepiness) == true)
            .ToArray();

        Assert.That(sleepinessInducingReagents, Is.Not.Empty);

        foreach (var reagent in sleepinessInducingReagents)
        {
            var effects = reagent.Metabolisms!.Metabolisms.Values
                .SelectMany(metabolism => metabolism.Effects);
            Assert.That(
                effects.OfType<MovementSpeedModifier>(),
                Is.Empty,
                $"{reagent.ID} should not configure MovementSpeedModifier; sleepiness slowdown is calculated by SleepinessSystem.");
        }
    }
}
