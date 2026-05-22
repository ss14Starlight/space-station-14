using System.Collections.Frozen;
using System.Linq;
using System.Text;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Genetics;

public sealed class GenesSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffectsSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

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
    /// Returns a random selection of traits with repetition.
    /// </summary>
    /// <returns>A random selection of traits with repetition.</returns>
    public IEnumerable<AbstractTraitPrototype> RandomTraits(Entity<GenesComponent> entity)
    {
        IEnumerable<AbstractTraitPrototype> traits = entity.Comp.AvailableTraits;
        while(true)
            yield return traits.ElementAt(_robustRandom.Next(0, traits.Count()));
    }

    public static TraitDict GetTraitsFromEnumerable(IEnumerable<Gene> genes) => TraitDict.Combine(genes.Select(g => g.Traits));

    public static TraitDict GetTraits(params Gene[] genes) => GetTraitsFromEnumerable(genes);

    public Gene GenerateGene(Entity<GenesComponent> entity)
    {
        TraitDict traitDict = new();
        var baseOffset = 0.5;

        var accumulatedValue = 0.0;
        foreach (var proto in RandomTraits(entity).Take(_robustRandom.Next(2, 6)))
        {
            var val = RandomGaussian(baseOffset - accumulatedValue, 0.25);
            traitDict.Traits[proto] = val;
            accumulatedValue += val;
        }

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

        return new Gene() { Traits = traitDict, TechnicalName = technicalName.ToString(), Name = null };
    }

    public void UpdateTraits(Entity<GenesComponent> entity)
    {
        var newTraits = GetTraitsFromEnumerable(entity.Comp.Genes).Traits;

        Dictionary<OnceTraitPrototype, FixedPoint2> newOnceTraits = new();
        Dictionary<OnSolutionChangedTraitPrototype, FixedPoint2> newOnSolutionChangedTraits = new();
        Dictionary<PassiveTraitPrototype, (FixedPoint2, TimeSpan)> newPassiveTraits = new();
        // I am not happy with this foreach loop. I really want to construct this instead from simple method calls on the IEnumerables.
        // But that's not possible because dictionaries don't have proper compatability with the OfType method.
        // See, when interpreted as an IEnumerable, which is necessary for access to the LINQ methods like OfType, the values become KeyValuePair
        // So if you do OfType, you try to cast KeyValuePair<T, ...> to KeyValuePair<X, ...>.
        // But that's not what you want, you want to cast T to X directly. There's no cast override that causes KeyValuePair to correctly interpret the above.
        // So the casts fail and you end up with nothing.
        // Thus I have to use this foreach loop instead.
        foreach (var t in newTraits)
        {
            if (!t.Key.Threshold.HasValue || t.Value >= t.Key.Threshold.Value)
            {
                if (t.Key is OnceTraitPrototype key1)
                    newOnceTraits.Add(key1, t.Value);
                else if (t.Key is OnSolutionChangedTraitPrototype key2)
                    newOnSolutionChangedTraits.Add(key2, t.Value);
                else if (t.Key is PassiveTraitPrototype key3)
                    newPassiveTraits.Add(key3, (t.Value, key3.Cooldown + _gameTiming.CurTime));
            }
        }

        var onceTraits = _entityManager.EnsureComponent<OnceTraitsComponent>(entity.Owner);
        var onSolutionChangedTraits = _entityManager.EnsureComponent<OnSolutionChangedTraitsComponent>(entity.Owner);
        var passiveTraits = _entityManager.EnsureComponent<PassiveTraitsComponent>(entity.Owner);

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
            _entityEffectsSystem.TryApplyEffect(entity.Owner, t.OnRemovedEffect.Effect,
                ((onceTraits.Traits[t] * t.OnRemovedEffect.ScalingFactor) + t.OnRemovedEffect.ScalingOffset).Float());
        }
        foreach (var t in newOnceTraits.Keys.Except(onceTraits.Traits.Keys))
        {
            _entityEffectsSystem.TryApplyEffect(entity.Owner, t.OnAddedEffect.Effect,
                ((newOnceTraits[t] * t.OnAddedEffect.ScalingFactor) + t.OnAddedEffect.ScalingOffset).Float());
        }
        foreach (var t in onceTraits.Traits.Keys.Intersect(newOnceTraits.Keys))
        {
            if (t.OnUpdatedEffect is null)
            {
                _entityEffectsSystem.TryApplyEffect(entity.Owner, t.OnRemovedEffect.Effect,
                    ((onceTraits.Traits[t] * t.OnRemovedEffect.ScalingFactor) + t.OnRemovedEffect.ScalingOffset).Float());
                _entityEffectsSystem.TryApplyEffect(entity.Owner, t.OnAddedEffect.Effect,
                    ((newOnceTraits[t] * t.OnAddedEffect.ScalingFactor) + t.OnAddedEffect.ScalingOffset).Float());
            }
            else
            {
                _entityEffectsSystem.TryApplyEffect(entity.Owner, t.OnUpdatedEffect.Effect,
                    ((newOnceTraits[t] * t.OnUpdatedEffect.ScalingFactor) + t.OnUpdatedEffect.ScalingOffset).Float());
            }
        }

        onceTraits.Traits = newOnceTraits;
        onSolutionChangedTraits.Traits = newOnSolutionChangedTraits;
        passiveTraits.Traits = newPassiveTraits;
    }
}

/// <summary>
/// A thin wrapper around a dictionary with specific support for dictionary combining.
/// New dictionaries are constructed with the old dictionaries unmodified.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class TraitDict
{
    public Dictionary<AbstractTraitPrototype, FixedPoint2> Traits = new();

    public TraitDict(Dictionary<AbstractTraitPrototype, FixedPoint2> traits) => Traits = traits;

    public TraitDict() {}

    public static TraitDict Combine(IEnumerable<TraitDict> traitDicts)
    {
        var newDict = new TraitDict();
        foreach (var dict in traitDicts)
        {
            foreach (var tm in dict.Traits)
            {
                if (newDict.Traits.TryGetValue(tm.Key, out var value))
                    newDict.Traits[tm.Key] = value + tm.Value;
                else
                    newDict.Traits[tm.Key] = tm.Value;
            }
        }

        return newDict;
    }

    public static TraitDict Add(params TraitDict[] traitDicts) => Combine(traitDicts);
}

/// <summary>
/// A collection of traits along with associated metadata, like the name.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class Gene
{
    /// <summary>
    /// The traits influenced by this gene.
    /// </summary>
    [DataField]
    public TraitDict Traits = new TraitDict();

    /// <summary>
    /// The unchanging "technical name" of a gene, i.e. PRKN (the name of a gene that creates the Parkin protein, mutations in which can cause parkinsons, hence the name).
    /// Can be procedurally generated.
    /// </summary>
    [DataField]
    public string TechnicalName = string.Empty;

    /// <summary>
    /// The informal name set by players and/or history.
    /// </summary>
    [DataField]
    public string? Name = string.Empty;
}
