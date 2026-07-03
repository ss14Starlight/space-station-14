using System.Linq;
using System.Text;
using Content.Shared._Starlight.Genetics.Components;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Genetics.Systems;

public sealed class GenesSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffectsSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <summary>
    /// Generates a random number from a Gaussian distribution. Achieved using a Box-Muller transform approximation. Good enough for most purposes. See also: https://stackoverflow.com/questions/218060/random-gaussian-variables
    /// </summary>
    /// <param name="mean">The mean of the distribution. Over time, the average of the numbers generated will be the mean.</param>
    /// <param name="stdDev">The standard deviation of the distribution. The higher, the more spread out the values.</param>
    /// <returns></returns>
    private double RandomGaussian(double mean, double stdDev)
    {
        var u1 = 1.0 - _robustRandom.NextDouble();
        var u2 = 1.0 - _robustRandom.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        var randNormal = mean + (stdDev * randStdNormal);
        return randNormal;
    }

    /// <summary>
    /// Implementation of the Marsaglia & Tsang method for generating numbers from a gamma distribution.
    /// Adapted from George Marsaglia and Wai Wan Tsang. 2000. A simple method for generating gamma variables. ACM Trans. Math. Softw. 26, 3 (Sept. 2000), 363–372. https://doi.org/10.1145/358407.358414
    /// </summary>
    /// <param name="alpha">The shape parameter of the gamma distribution.</param>
    /// <returns></returns>
    public double RandomGamma(double alpha)
    {
        if (alpha < 1.0)
        {
            double u = _robustRandom.NextDouble();
            return RandomGamma(alpha + 1.0) * Math.Pow(u, 1.0 / alpha);
        }

        while (true)
        {
            var d = alpha - (1.0 / 3.0);
            var c = 1.0 / Math.Sqrt(9.0 * d);

            var x = RandomGaussian(0.0, 1.0);
            var v1 = 1.0 + (c * x);
            var v = v1 * v1 * v1;
            while (v <= 0.0)
            {
                x = RandomGaussian(0.0, 1.0);
                v1 = (1.0 + (c * x));
                v = v1 * v1 * v1;
            }

            var U = _robustRandom.NextDouble();
            if (U < 1 - (0.0331 * (x * x * x * x))) return d * v;
            if (Math.Log(U) < (0.5 * x * x) + (d * (1 - v + Math.Log(v)))) return d * v;
        }
    }

    /// <summary>
    /// Returns a random selection of traits with repetition.
    /// </summary>
    /// <returns>A random selection of traits with repetition.</returns>
    public IEnumerable<ProtoId<AbstractTraitPrototype>> RandomTraits(Entity<GenesComponent> entity)
    {
        IEnumerable<ProtoId<AbstractTraitPrototype>> traits = entity.Comp.AvailableTraits;
        while(true)
            yield return traits.ElementAt(_robustRandom.Next(0, traits.Count()));
    }

    public static TraitDict GetTraitsFromEnumerable(IEnumerable<Gene> genes) => TraitDict.Combine(genes.Select(g => g.Traits));

    public static TraitDict GetTraits(params Gene[] genes) => GetTraitsFromEnumerable(genes);

    public Gene GenerateGene(Entity<GenesComponent> entity)
    {
        var shape = 0.7;
        var target_sum = _robustRandom.NextDouble(0.5, 2.0);
        var magnitude_budget = Math.Abs(target_sum) + _robustRandom.NextDouble(1.0, 3.0);
        var P = (magnitude_budget + target_sum) / 2.0;
        var N = (magnitude_budget - target_sum) / 2.0;

        var amt = _robustRandom.Next(2, 6);

        var protoList = RandomTraits(entity).Take(amt).ToList();
        var positiveList = Enumerable.Range(0, amt).Select(_ => RandomGamma(shape)).ToList();
        var positiveListSum = positiveList.Sum();
        positiveList = positiveList.Select(x => (x * P) / positiveListSum).ToList();
        var negativeList = Enumerable.Range(0, amt).Select(_ => RandomGamma(shape)).ToList();
        var negativeListSum = positiveList.Sum();
        negativeList = negativeList.Select(x => (x * N) / negativeListSum).ToList();

        TraitDict traitDict = new();

        for (var i = 0; i < amt; i++) traitDict.Traits[protoList[i]] = positiveList[i] - negativeList[i];

        // This is an overly fancy way to generate technical-looking names. But meh.
        string[] uppercaseAlphabet = ["A", "C", "G", "T", "R", "Q", "M", "P"];
        string[] lowercaseAlphabet = ["u", "v", "m", "n", "p", "w", "a", "k"];

        StringBuilder technicalName = new();
        var limit = _robustRandom.Next(0, 4);
        for (var i = 0; i < limit; i++)
            technicalName.Append(uppercaseAlphabet[_robustRandom.Next(0, uppercaseAlphabet.Length)]);
        limit = _robustRandom.Next(0, 5);
        for (var i = 0; i < limit; i++)
            technicalName.Append(lowercaseAlphabet[_robustRandom.Next(0, lowercaseAlphabet.Length)]);
        limit = _robustRandom.Next(1, 4);
        for (var i = 0; i < limit; i++)
            technicalName.Append(uppercaseAlphabet[_robustRandom.Next(0, uppercaseAlphabet.Length)]);
        technicalName.Append(_robustRandom.Next(1, 9));

        return new Gene { Traits = traitDict, TechnicalName = technicalName.ToString(), Name = null };
    }

    public void UpdateTraits(Entity<GenesComponent> entity)
    {
        var newTraits = GetTraitsFromEnumerable(entity.Comp.Genes).Traits;

        Dictionary<ProtoId<OnceTraitPrototype>, FixedPoint2> newOnceTraits = new();
        Dictionary<ProtoId<OnSolutionChangedTraitPrototype>, FixedPoint2> newOnSolutionChangedTraits = new();
        Dictionary<ProtoId<PassiveTraitPrototype>, FixedPoint2> newPassiveTraits = new();
        Dictionary<ProtoId<PassiveTraitPrototype>, TimeSpan> newPassiveTraitsCooldowns = new();
        foreach (var t in newTraits)
        {
            if (_prototypeManager.TryIndex<OnceTraitPrototype>(t.Key, out var proto1) &&
                (!proto1.Threshold.HasValue || t.Value >= proto1.Threshold.Value) && entity.Comp.Classes.Intersect(proto1.Classes).Any())
                newOnceTraits.Add(proto1, t.Value);
            else if (_prototypeManager.TryIndex<OnSolutionChangedTraitPrototype>(t.Key, out var proto2) &&
                     (!proto2.Threshold.HasValue || t.Value >= proto2.Threshold.Value) && entity.Comp.Classes.Intersect(proto2.Classes).Any())
                newOnSolutionChangedTraits.Add(proto2, t.Value);
            else if (_prototypeManager.TryIndex<PassiveTraitPrototype>(t.Key, out var proto3) &&
                     (!proto3.Threshold.HasValue || t.Value >= proto3.Threshold.Value) && entity.Comp.Classes.Intersect(proto3.Classes).Any())
            {
                newPassiveTraits.Add(proto3, t.Value);
                newPassiveTraitsCooldowns.Add(proto3, proto3.Cooldown + _gameTiming.CurTime);
            }

        }

        if (_entityManager.TryGetComponent<Components.OnceTraitsComponent>(entity, out var onceTraits))
        {
            /*
             * Okay, this is going to be ugly.
             * The once traits need to be updated in the following way:
             * 1. If a trait exists in the old list but not in the new list, call onRemoved
             * 2. If a trait does not exist in the old list but does exist in the new list, call onAdded
             * 3. If a trait exists in both lists AND the trait has an onUpdated, call onUpdated
             * 4. If a trait exists in both lists AND the trait does not have an onUpdated, call onRemoved and then onAdded
             * wait, I can handle all of this with sets, what am I doing?
             * 1 is O - N
             * 2 is N - O
             * 3 and 4 are both O /\ N
             */

            foreach (var t in onceTraits.Traits.Keys.Except(newOnceTraits.Keys))
            {
                var trait = _prototypeManager.Index(t);
                _entityEffectsSystem.TryApplyEffect(entity.Owner, trait.OnRemovedEffect.Effect,
                    ((onceTraits.Traits[t] * trait.OnRemovedEffect.ScalingFactor) + trait.OnRemovedEffect.ScalingOffset).Float());
            }
            foreach (var t in newOnceTraits.Keys.Except(onceTraits.Traits.Keys))
            {
                var trait = _prototypeManager.Index(t);
                _entityEffectsSystem.TryApplyEffect(entity.Owner, trait.OnAddedEffect.Effect,
                    ((newOnceTraits[t] * trait.OnAddedEffect.ScalingFactor) + trait.OnAddedEffect.ScalingOffset).Float());
            }
            foreach (var t in onceTraits.Traits.Keys.Intersect(newOnceTraits.Keys))
            {
                var trait = _prototypeManager.Index(t);
                if (trait.OnUpdatedEffect is null)
                {
                    _entityEffectsSystem.TryApplyEffect(entity.Owner, trait.OnRemovedEffect.Effect,
                        ((onceTraits.Traits[t] * trait.OnRemovedEffect.ScalingFactor) + trait.OnRemovedEffect.ScalingOffset).Float());
                    _entityEffectsSystem.TryApplyEffect(entity.Owner, trait.OnAddedEffect.Effect,
                        ((newOnceTraits[t] * trait.OnAddedEffect.ScalingFactor) + trait.OnAddedEffect.ScalingOffset).Float());
                }
                else
                {
                    _entityEffectsSystem.TryApplyEffect(entity.Owner, trait.OnUpdatedEffect.Effect,
                        ((newOnceTraits[t] * trait.OnUpdatedEffect.ScalingFactor) + trait.OnUpdatedEffect.ScalingOffset).Float());
                }
            }

            onceTraits.Traits = newOnceTraits;
        }

        if (_entityManager.TryGetComponent<Components.OnSolutionChangedTraitsComponent>(entity, out var onSolutionChangedTraits))
            onSolutionChangedTraits.Traits = newOnSolutionChangedTraits;

        if (_entityManager.TryGetComponent<Components.PassiveTraitsComponent>(entity, out var passiveTraits))
        {
            passiveTraits.Traits = newPassiveTraits;
            passiveTraits.Cooldowns = newPassiveTraitsCooldowns;
        }
    }
}
