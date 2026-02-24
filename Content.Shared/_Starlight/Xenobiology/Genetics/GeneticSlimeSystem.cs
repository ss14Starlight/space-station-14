using System.Collections.Frozen;
using System.Linq;
using System.Text;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Xenobiology.Genetics;

/// <summary>
/// Generates the genes at round start and then supplies the genes for the rest of the round.
/// </summary>
public sealed class GeneticSlimeSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly PrototypeManager _prototypeManager = default!;

    private FrozenDictionary<string, AbstractXenobiologyTraitPrototype>? _genes;
    
    public override void Initialize()
    {
        base.Initialize();
        
        _genes = _prototypeManager.GetInstances<AbstractXenobiologyTraitPrototype>();
    }

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
    /// Returns the collection of genes in random order.
    /// </summary>
    /// <returns>An enumerator over the random genes.</returns>
    public IEnumerable<KeyValuePair<string, AbstractXenobiologyTraitPrototype>> RandomGenes()
    {
        if (_genes is null) yield break;
        while(true)
            yield return _genes.ElementAt(_robustRandom.Next(0, _genes.Count));
    }


    private Gene GenerateGene()
    {
        TraitDict traitDict = new();
        var baseOffset = 0.5;

        var accumulatedValue = 0.0;
        foreach (var proto in RandomGenes().Take(_robustRandom.Next(2, 6)))
        {
            var val = RandomGaussian(baseOffset - accumulatedValue, 0.25);
            traitDict.Traits[proto.Key] = val;
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
}

/// <summary>
/// A thin wrapper around a dictionary with specific support for dictionary combining.
/// New dictionaries are constructed with the old dictionaries unmodified.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class TraitDict
{
    public Dictionary<ProtoId<AbstractXenobiologyTraitPrototype>, FixedPoint2> Traits = new();

    public TraitDict(Dictionary<ProtoId<AbstractXenobiologyTraitPrototype>, FixedPoint2> traits) => Traits = traits;

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