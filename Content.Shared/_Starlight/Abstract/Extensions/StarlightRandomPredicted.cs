using System.Linq;
using JetBrains.Annotations;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Abstract.Extensions;

/// This exists solely because I was told not to put this in <see cref="RandomPredicted"/> due to something about an unmerged RT pull request.
public static class StarlightRandomPredicted
{
    /// <summary>
    /// Picks a predictable random element from a collection based on a set of weights.
    /// </summary>
    /// <param name="random">The <see cref="IRobustRandom"/> instance.</param>
    /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
    /// <param name="weights">A dictionary where the keys are the list to pick from and the values are the weights.</param>
    /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
    /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
    [PublicAPI]
    public static T PickPredicted<T>(this IRobustRandom random, IGameTiming timing, Dictionary<T, float> weights, int seed = 0)
        where T: notnull
    {
        var sum = weights.Values.Sum();
        var accumulated = 0f;

        var rand = random.NextFloatPredicted(timing, seed) * sum;

        foreach (var (key, weight) in weights)
        {
            accumulated += weight;

            if (accumulated >= rand)
            {
                return key;
            }
        }

        throw new InvalidOperationException("Invalid weighted pick");
    }
}
