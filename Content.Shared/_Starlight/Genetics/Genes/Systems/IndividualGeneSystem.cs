using System.Linq;
using System.Text;
using System.Threading;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared._Starlight.Genetics.Genes.Components;
using Content.Shared._Starlight.Genetics.Genes.Prototypes;
using Content.Shared._Starlight.Genetics.GeneticTraits.Prototypes;
using Content.Shared.GameTicking;
using Content.Shared.Random.Helpers;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Shared._Starlight.Genetics.Genes.Systems;

public sealed partial class IndividualGeneSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private IPrototypeManager _proto = null!;

    /// <summary>
    /// Keeps track of genes that were generated this round, associating generated
    /// technical name to DNA sequence to keep things consistent throughout the round.
    /// </summary>
    private readonly Dictionary<string, string> _existingGenes = [];

    /// <summary>
    /// Collection of genes that were previously removed from a container have yet to be inserted into a new one.
    /// If any gene entities are in this state and thus present in this collection for more than 20 seconds, they
    /// are deleted to avoid unnecessary entities being piled up until round restart.
    /// </summary>
    private readonly Dictionary<EntityUid, CancellationTokenSource> _unparentedGenes = [];

    private const int GarbageCheckInterval = 20;

    [SubscribeLocalEvent]
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args) => _existingGenes.Clear();

    [SubscribeLocalEvent]
    private void OnContainerRemove(Entity<IndividualGeneComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        var source = new CancellationTokenSource();
        _unparentedGenes.Add(ent, source);
        Timer.Spawn(TimeSpan.FromSeconds(GarbageCheckInterval), () =>
        {
            if (!_unparentedGenes.Remove(ent)) return;
            Log.Info(
                $"Genome entity {ToPrettyString(ent)} being cleaned up due to not being in any container for 20 seconds.");
            PredictedQueueDel(ent);
        }, source.Token);
    }

    [SubscribeLocalEvent]
    private void OnContainerInsert(Entity<IndividualGeneComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!_unparentedGenes.Remove(ent, out var source)) return;
        source.Cancel();
    }

    /// <summary>
    /// Generates a random number from a Gaussian distribution. Achieved using a Box-Muller transform approximation. Good enough for most purposes. See also: https://stackoverflow.com/questions/218060/random-gaussian-variables
    /// </summary>
    /// <param name="rand">random instance</param>
    /// <param name="mean">The mean of the distribution. Over time, the average of the numbers generated will be the mean.</param>
    /// <param name="stdDev">The standard deviation of the distribution. The higher, the more spread out the values.</param>
    /// <returns></returns>
    private static double RandomGaussian(System.Random rand, double mean, double stdDev)
    {
        var u1 = 1.0 - rand.NextDouble();
        var u2 = 1.0 - rand.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        var randNormal = mean + (stdDev * randStdNormal);
        return randNormal;
    }

    /// <summary>
    /// Implementation of the Marsaglia & Tsang method for generating numbers from a gamma distribution.
    /// Adapted from George Marsaglia and Wai Wan Tsang. 2000. A simple method for generating gamma variables. ACM Trans. Math. Softw. 26, 3 (Sept. 2000), 363–372. https://doi.org/10.1145/358407.358414
    /// </summary>
    /// <param name="rand">random instance</param>
    /// <param name="alpha">The shape parameter of the gamma distribution.</param>
    /// <returns></returns>
    public static double RandomGamma(System.Random rand, double alpha)
    {
        if (alpha < 1.0)
        {
            var u = rand.NextDouble();
            return RandomGamma(rand, alpha + 1.0) * Math.Pow(u, 1.0 / alpha);
        }

        while (true)
        {
            var d = alpha - (1.0 / 3.0);
            var c = 1.0 / Math.Sqrt(9.0 * d);

            var x = RandomGaussian(rand, 0.0, 1.0);
            var v1 = 1.0 + (c * x);
            var v = v1 * v1 * v1;
            while (v <= 0.0)
            {
                x = RandomGaussian(rand, 0.0, 1.0);
                v1 = 1.0 + (c * x);
                v = v1 * v1 * v1;
            }

            var u = rand.NextDouble();
            if (u < 1 - (0.0331 * (x * x * x * x)) || Math.Log(u) < (0.5 * x * x) + (d * (1 - v + Math.Log(v))))
                return d * v;
        }
    }

    /// <summary>
    /// Returns a random selection of traits with repetition.
    /// </summary>
    /// <returns>A random selection of traits with repetition.</returns>
    public static IEnumerable<ProtoId<GeneticTraitPrototype>> RandomTraits(System.Random rand, Entity<GenesComponent> entity, int count)
    {
        IEnumerable<ProtoId<GeneticTraitPrototype>> traits = entity.Comp.AvailableTraits;
        for (var i = 0; i < count; i++)
            yield return traits.ElementAt(rand.Next(0, traits.Count()));
    }

    public Entity<IndividualGeneComponent>? GeneFromSample(ProtoId<SampleGenePrototype> protoId)
    {
        if (!_proto.TryIndex(protoId, out var proto)) return null;
        return GeneFromSample(proto);
    }

    public Entity<IndividualGeneComponent> GeneFromSample(SampleGenePrototype proto)
    {
        var newGene = PredictedSpawn("Gene");
        EnsureComp<IndividualGeneComponent>(newGene, out var individualGene);
        individualGene.Traits = proto.Traits;
        individualGene.TechnicalName = proto.TechnicalName;
        individualGene.Name = proto.Name;
        individualGene.Prototype = proto.ID;
        _meta.SetEntityName(newGene, $"{proto.Name} ({proto.TechnicalName})");
        if (proto.Sequence is not null)
        {
            individualGene.Sequence = proto.Sequence;
            _existingGenes.Add(individualGene.TechnicalName, proto.Sequence);
        }
        else
        {
            var sequence = GetDnaSequence(individualGene.TechnicalName);
            individualGene.Sequence = sequence;
        }
        // _geneSingletons.Add(protoId, (newGene, individualGene));
        Dirty(newGene, individualGene);
        return (newGene, individualGene);
    }

    public Entity<IndividualGeneComponent> GenerateGeneFromEntity(Entity<GenesComponent> entity)
    {
        var rand = _random.GetPredictedRandom(_timing, GetNetEntity(entity).Id);

        const double Shape = 0.7;
        var targetSum = 0.5 + (rand.NextDouble() * 1.5);
        var magnitudeBudget = Math.Abs(targetSum) + 6.0 + (rand.NextDouble() * 2.0);
        var p = (magnitudeBudget + targetSum) / 2.0;
        var n = (magnitudeBudget - targetSum) / 2.0;

        var amt = rand.Next(2, 6);

        var protoList = RandomTraits(rand, entity, amt).ToList();
        var positiveList = Enumerable.Range(0, amt).Select(_ => RandomGamma(rand, Shape)).ToList();
        var positiveListSum = positiveList.Sum();
        positiveList = [.. positiveList.Select(x => x * p / positiveListSum)];
        var negativeList = Enumerable.Range(0, amt).Select(_ => RandomGamma(rand, Shape)).ToList();
        var negativeListSum = negativeList.Sum();
        negativeList = [.. negativeList.Select(x => x * n / negativeListSum)];

        TraitDict traitDict = new();

        for (var i = 0; i < amt; i++) traitDict.Traits[protoList[i]] = positiveList[i] - negativeList[i];

        var gene = GenerateGene();
        gene.Comp.Traits = traitDict;
        _meta.SetEntityName(gene, gene.Comp.TechnicalName);
        Dirty(gene);

        return gene;
    }

    public Entity<IndividualGeneComponent> GenerateGeneFromTraits(TraitDict traits, string? name = null)
    {
        var gene = GenerateGene();
        gene.Comp.Traits = traits;
        gene.Comp.Name = name;
        _meta.SetEntityName(gene, name is not null
            ? $"{name} ({gene.Comp.TechnicalName})"
            : gene.Comp.TechnicalName);
        Dirty(gene);

        return gene;
    }

    private Entity<IndividualGeneComponent> GenerateGene()
    {
        var rand = _random.GetPredictedRandom(_timing);

        // This is an overly fancy way to generate technical-looking names. But meh.
        string[] uppercaseAlphabet = ["A", "C", "G", "T", "R", "Q", "M", "P"];
        string[] lowercaseAlphabet = ["u", "v", "m", "n", "p", "w", "a", "k"];

        StringBuilder technicalName = new();
        var limit = rand.Next(0, 4);
        for (var i = 0; i < limit; i++)
            technicalName.Append(uppercaseAlphabet[rand.Next(uppercaseAlphabet.Length)]);
        limit = rand.Next(0, 5);
        for (var i = 0; i < limit; i++)
            technicalName.Append(lowercaseAlphabet[rand.Next(lowercaseAlphabet.Length)]);
        limit = rand.Next(1, 4);
        for (var i = 0; i < limit; i++)
            technicalName.Append(uppercaseAlphabet[rand.Next(uppercaseAlphabet.Length)]);
        technicalName.Append(rand.Next(1, 9));

        var newGene = PredictedSpawn("Gene");
        EnsureComp<IndividualGeneComponent>(newGene, out var individualGene);
        individualGene.TechnicalName = technicalName.ToString();
        individualGene.Sequence = GetDnaSequence(technicalName.ToString());

        return (newGene, individualGene);
    }

    public string GetDnaSequence(string techName)
    {
        if (_existingGenes.TryGetValue(techName, out var existing))
            return existing;

        var rand = _random.GetPredictedRandom(_timing, SharedRandomExtensions.HashCodeCombine(techName.Select(c => (int) c).ToArray()));

        char[] bases = ['A', 'T', 'C', 'G'];
        var length = 16;

        // Rare chance to generate with a longer or shorter sequence, for funsies!
        if (rand.NextDouble() < 0.08)
        {
            var sign = rand.Next(2) == 0 ? 1 : -1;
            length += sign * rand.Next(1, 9);
        }

        var builder = new StringBuilder();
        for (var i = 0; i < length; i++)
            builder.Append(bases[rand.Next(bases.Length)]);

        var sequence = builder.ToString();

        _existingGenes.Add(techName, sequence);
        return sequence;
    }

    /// <summary>
    /// Clones the data of one genome to a new genome instance.
    /// </summary>
    public Entity<IndividualGeneComponent> Clone(Entity<IndividualGeneComponent> ent)
    {
        // if cloning a base gene that exists as a prototype and doesn't have any changes, just do the other method
        if (ent.Comp.Prototype is not null)
        {
            var sampled = GeneFromSample(ent.Comp.Prototype.Value);
            if (sampled is null)
                throw new NullReferenceException(
                    "Attempted to clone a gene with a set prototype but came back as null.");
            return sampled.Value;
        }
        var clone = PredictedSpawn();
        var comp = EnsureComp<IndividualGeneComponent>(clone);
        comp.Name = ent.Comp.Name;
        comp.TechnicalName = ent.Comp.TechnicalName;
        comp.Sequence = ent.Comp.Sequence;
        comp.Traits = new TraitDict { Traits = ent.Comp.Traits.Traits.ToDictionary() };
        return (clone, comp);
    }

    public IEnumerable<Entity<IndividualGeneComponent>> Clone(IEnumerable<Entity<IndividualGeneComponent>> entities) =>
        entities.Select(Clone);

    public IEnumerable<EntityUid> Clone(IEnumerable<EntityUid> uids) =>
        uids.Select(uid =>
            !TryComp<IndividualGeneComponent>(uid, out var gene)
                ? throw new Exception($"Tried to clone entity {ToPrettyString(uid)}, which was not a gene.")
                : Clone((uid, gene))).Select(ent => ent.Owner);

    // Technically doesn't check if they're truly identical, just checks if the tech names are since those are generated once and only ever copied.
    public bool IsIdentical(Entity<IndividualGeneComponent> ent1, Entity<IndividualGeneComponent> ent2) =>
        ent1.Comp.TechnicalName == ent2.Comp.TechnicalName;
}
