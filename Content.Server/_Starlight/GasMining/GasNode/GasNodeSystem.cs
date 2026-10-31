using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.GasMining.GasNode;
using Content.Shared.Atmos;
using Robust.Shared.Random;

namespace Content.Server._Starlight.GasMining.GasNode;

public sealed partial class GasNodeSystem : SharedGasNodeSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    enum GasPurity
    {
        PURE,
        ALMOST_PURE,
        SOMEWHAT_PURE,
        NOT_PURE,
        RANDOM_MIXTURE
    }

    // Keep this in order with the GasPurity enums or RollPurity will have UB
    private static readonly Dictionary<GasPurity, int> _weights = new()
    {
        { GasPurity.PURE, 5 },
        { GasPurity.ALMOST_PURE, 15 },
        { GasPurity.SOMEWHAT_PURE, 30 },
        { GasPurity.NOT_PURE, 40 },
        { GasPurity.RANDOM_MIXTURE, 10 }
    };

    private static int TotalPurityWeight()
    {
        var totalWeight = 0;

        foreach (var weight in _weights.Values)
        {
            totalWeight += weight;
        }

        return totalWeight;
    }

    private GasPurity RollPurity()
    {
        var purityRoll = _random.Next(0, TotalPurityWeight());

        var purityEnum = 0;
        var totalWeight = 0;
        foreach (var weights in _weights.Values)
        {
            totalWeight += weights;
            if (totalWeight >= purityRoll)
            {
                break;
            }
            else
            {
                purityEnum++;
            }
        }

        return (GasPurity)purityEnum;
    }

    [SubscribeLocalEvent]
    public void OnGasNodeGeneration(
        Entity<GasNodeComponent> entity,
        ref ComponentInit args
    )
    {
        var tempRand = _random.NextFloat();
        var temp = 2.7f + MathF.Pow(370.45f * (tempRand / (1.0f - tempRand)), 0.45f) + (74626.95f * MathF.Pow(tempRand, 15));
        temp = float.Clamp(temp, 2.7f, 75000f);
        entity.Comp.Temperature = temp;

        var purity = RollPurity();

        if (purity != GasPurity.RANDOM_MIXTURE)
        {
            var primaryGas = (Gas)_random.Next(0, Atmospherics.TotalNumberOfGases);
            var purityPercentage = 0.0f;

            if (purity == GasPurity.PURE)
            {
                purityPercentage = 1f;
            } else if (purity == GasPurity.ALMOST_PURE)
            {
                purityPercentage = _random.NextFloat(0.85f, 1.0f);
            } else if (purity == GasPurity.SOMEWHAT_PURE)
            {
                purityPercentage = _random.NextFloat(0.70f, 0.85f);
            } else if (purity == GasPurity.NOT_PURE)
            {
                purityPercentage = _random.NextFloat(0.40f, 0.70f);
            }

            var inertGasGuard = true;
            while (inertGasGuard)
            {
                var mixture = GenerateGasMixture(primaryGas, purityPercentage);
                var testMixture = new GasMixture
                {
                    Temperature = temp,
                    Volume = 10000f
                };
                foreach (var gas in mixture)
                {
                    testMixture.AdjustMoles(gas.Key, gas.Value);
                }

                var result = _atmosphere.React(testMixture, null);

                if (result == Shared.Atmos.Reactions.ReactionResult.NoReaction)
                {
                    entity.Comp.Gases = mixture;
                    return;
                }
            }
        } else
        {
            var inertGasGuard = true;
            while (inertGasGuard)
            {
                var mixture = GenerateGasMixture(null, null);
                var testMixture = new GasMixture
                {
                    Temperature = temp,
                    Volume = 10000f,
                };
                foreach (var gas in mixture)
                {
                    testMixture.AdjustMoles(gas.Key, gas.Value);
                }

                var result = _atmosphere.React(testMixture, null);

                if (result == Shared.Atmos.Reactions.ReactionResult.NoReaction)
                {
                    entity.Comp.Gases = mixture;
                    return;
                }
            }
        }
    }

    private Dictionary<Gas, float> GenerateGasMixture(Gas? primaryGas, float? purityPrimaryGas)
    {
        Dictionary<Gas, float> Gases = [];

        var totalMoles = float.Clamp((float)_random.NextGaussian(50000f, 15000f), 10000f, 100000f);
        var remainingPercent = 1.0f;

        int numContaminants;

        if (primaryGas is not null)
        {
            if (purityPrimaryGas is >= 1.0f or null)
            {
                Gases.Add((Gas)primaryGas, totalMoles);
                return Gases;
            }
            else
            {
                Gases.Add((Gas)primaryGas, totalMoles * (float)purityPrimaryGas);
                remainingPercent -= (float)purityPrimaryGas;
            }
        }

        // Adding a '+ 1' to disambiguate the real intended number since _random.Next() is exclusive max
        if (remainingPercent < 0.3)
        {
            numContaminants = _random.Next(1, 3 + 1);
        }
        else if (remainingPercent < 0.5)
        {
            numContaminants = _random.Next(2, 5 + 1);
        }
        else if (remainingPercent < 0.7)
        {
            numContaminants = _random.Next(2, 7 + 1);
        }
        else if (remainingPercent < 0.9)
        {
            numContaminants = _random.Next(3, 9 + 1);
        }
        else
        {
            numContaminants = _random.Next(4, 10 + 1);
        }

        var contaminants = new Gas[numContaminants];

        for (var i = 0; i < numContaminants; i++)
        {
            contaminants[i] = (Gas) _random.Next(0, Atmospherics.TotalNumberOfGases);
            // Try again if the contaminant is the same as the primary gas
            if (contaminants[i] == primaryGas)
            {
                i--;
                continue;
            }
        }

        for (var i = 0; i < numContaminants; i++)
        {
            if (i == numContaminants - 1)
            {
                if (!Gases.TryAdd(contaminants[i], totalMoles * remainingPercent))
                {
                    Gases[contaminants[i]] += totalMoles * remainingPercent;
                }
            } else
            {
                var contaminantPercentage = _random.NextFloat(0.01f, remainingPercent / 2f);
                if (!Gases.TryAdd(contaminants[i], totalMoles * contaminantPercentage))
                {
                    Gases[contaminants[i]] += totalMoles * remainingPercent;
                }
                remainingPercent -= contaminantPercentage;
            }
        }

        return Gases;
    }

    [SubscribeLocalEvent]
    private void OnAnalyzed(Entity<GasNodeComponent> entity, ref GasAnalyzerScanEvent args)
    {
        args.GasMixtures ??= [];

        var gasMixture = new GasMixture
        {
            Temperature = entity.Comp.Temperature,
            Volume = 50000f
        };

        foreach (var gas in entity.Comp.Gases)
        {
            gasMixture.AdjustMoles(gas.Key, gas.Value);
        }

        args.GasMixtures.Add((Name(entity.Owner), gasMixture));
    }
}
