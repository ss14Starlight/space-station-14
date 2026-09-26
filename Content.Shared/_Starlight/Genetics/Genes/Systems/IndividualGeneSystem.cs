using System.Linq;
using System.Text;
using Content.Shared._Starlight.Genetics.Components;
using Content.Shared._Starlight.Genetics.Genes.Components;
using Content.Shared._Starlight.Genetics.Genes.Prototypes;
using Content.Shared._Starlight.Genetics.GeneticTraits.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Genetics.Genes.Systems;

public sealed partial class IndividualGeneSystem : EntitySystem
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    private Dictionary<ProtoId<SampleGenePrototype>, Entity<IndividualGeneComponent>> _geneSingletons = new();

    private TimeSpan _nextGC = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        CleanUnusedGenes();
    }

    private void CleanUnusedGenes()
    {
        if (_gameTiming.CurTime > _nextGC)
        {
            // Practically an implementation of mark-and-sweep
            // Specifically, we go over each gene, identify the ones that are held, and discard all the one that aren't
            var allGenes = new HashSet<EntityUid>();
            var ge = _entityManager.EntityQueryEnumerator<IndividualGeneComponent>();
            while (ge.MoveNext(out var uid, out var individualGeneComponent)) allGenes.Add(uid);
            var geneUsers = _entityManager.EntityQueryEnumerator<GenesComponent>();
            while (geneUsers.MoveNext(out var uid, out var genesComponent))
                foreach (var gene in genesComponent.Genes.Where(allGenes.Contains))
                    allGenes.Remove(gene);
            var geneConsoles = _entityManager.EntityQueryEnumerator<GeneticsConsoleComponent>();
            while (geneConsoles.MoveNext(out var uid, out var geneticsConsoleComponent))
                foreach (var gene in geneticsConsoleComponent.Genes.Where(allGenes.Contains))
                    allGenes.Remove(gene);
            var geneSamplers = _entityManager.EntityQueryEnumerator<GeneSamplerComponent>();
            while (geneSamplers.MoveNext(out var uid, out var geneSamplerComponent))
                foreach (var gene in geneSamplerComponent.Genes.Where(allGenes.Contains))
                    allGenes.Remove(gene);

            foreach (var gene in allGenes)
            {
                if (_entityManager.TryGetComponent<IndividualGeneComponent>(gene, out var geneComponent))
                    if (geneComponent.Prototype != null)
                        _geneSingletons.Remove(geneComponent.Prototype.Value);
                _entityManager.PredictedQueueDeleteEntity(gene);
            }

            _nextGC = _gameTiming.CurTime + TimeSpan.FromMinutes(10);
        }
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
    public IEnumerable<ProtoId<GeneticTraitPrototype>> RandomTraits(Entity<GenesComponent> entity)
    {
        IEnumerable<ProtoId<GeneticTraitPrototype>> traits = entity.Comp.AvailableTraits;
        while(true)
            yield return traits.ElementAt(_robustRandom.Next(0, traits.Count()));
    }

    public Entity<IndividualGeneComponent> GenerateGene(Entity<GenesComponent> entity)
    {
        var shape = 0.7;
        var target_sum = _robustRandom.NextDouble(0.5, 2.0);
        var magnitude_budget = Math.Abs(target_sum) + _robustRandom.NextDouble(6.0, 8.0);
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

        var newGene = _entityManager.PredictedSpawn("Gene");
        _entityManager.EnsureComponent<IndividualGeneComponent>(newGene, out var individualGene);
        individualGene.Traits = traitDict;
        individualGene.TechnicalName = technicalName.ToString();
        individualGene.Name = null;

        return (newGene, individualGene);
    }

    public Entity<IndividualGeneComponent> GeneFromSample(ProtoId<SampleGenePrototype> protoId)
    {
        if (_geneSingletons.TryGetValue(protoId, out var sample)) return sample;

        var proto = _prototypeManager.Index(protoId);
        var newGene = _entityManager.PredictedSpawn("Gene");
        _entityManager.EnsureComponent<IndividualGeneComponent>(newGene, out var individualGene);
        individualGene.Traits = proto.Traits;
        individualGene.TechnicalName = proto.TechnicalName;
        individualGene.Name = proto.Name;
        individualGene.Prototype = protoId;
        _geneSingletons.Add(protoId, (newGene, individualGene));
        return (newGene, individualGene);
    }
}
