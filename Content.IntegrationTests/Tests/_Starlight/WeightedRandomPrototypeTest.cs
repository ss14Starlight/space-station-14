using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight;

public sealed class WeightedRandomPrototypeTest : GameTest
{
    private const float RelativeTolerance = 0.00001f;

    [Test]
    public async Task WeightTotalsArePowersOfTen()
    {
        var server = Pair.Server;
        var protoMan = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                var weightedRandomKinds = protoMan.EnumeratePrototypeKinds()
                    .Where(type => typeof(IWeightedRandomPrototype).IsAssignableFrom(type));

                foreach (var kind in weightedRandomKinds)
                {
                    foreach (var prototype in protoMan.EnumeratePrototypes(kind).Cast<IWeightedRandomPrototype>())
                    {
                        AssertPowerOfTen(prototype, prototype.Weights.Values.Sum());
                    }
                }

                foreach (var prototype in protoMan.EnumeratePrototypes<WeightedRandomFillSolutionPrototype>())
                {
                    AssertPowerOfTen(prototype, prototype.Fills.Sum(fill => fill.Weight));
                }
            }
        });
    }

    private static void AssertPowerOfTen(IPrototype prototype, float totalWeight) =>
        Assert.That(IsPowerOfTen(totalWeight),
            $"The weights for {prototype.GetType().Name} \"{prototype.ID}\" total {totalWeight}, which is not a power of 10.");

    private static bool IsPowerOfTen(float value)
    {
        if (!float.IsFinite(value) || value <= 0f)
            return false;

        var closestPower = MathF.Pow(10f, MathF.Round(MathF.Log10(value)));
        return MathF.Abs(value - closestPower) <= closestPower * RelativeTolerance;
    }
}
