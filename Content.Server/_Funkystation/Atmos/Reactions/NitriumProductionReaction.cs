using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Funkystation.Atmos.Reactions;

/// <summary>
///     Funky Atmos - /tg/ gases
///     Produces Nitrium by mixing Tritium, Nitrogen and Pluoxium at temperatures above 500K.
/// </summary>
[UsedImplicitly]
public sealed partial class NitriumProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (mixture.Temperature > 20f && mixture.GetMoles(Gas.HyperNoblium) >= 5f)
            return ReactionResult.NoReaction;

        var initTritium = mixture.GetMoles(Gas.Tritium);
        var initNitrogen = mixture.GetMoles(Gas.Nitrogen);
        var initPluox = mixture.GetMoles(Gas.Pluoxium);
        var initBZ = mixture.GetMoles(Gas.BZ);
        var pressure = mixture.Pressure;
        var volume = mixture.Volume;
        var temperature = mixture.Temperature;

/// Check what ingredient is smallest relative to its un-catalyzed demand. Use it as a limiter.

        var limit = Math.Min(initTritium / 2f, Math.Min(initNitrogen / 3f, initPluox));

/// Produces faster with higher temperature, lower pressure, and higher concetrations of BZ. BZ also magnifies the nitrogen consumption so watch out.

        var tempRate = temperature/ 500f ;

        var pressureRate = (100f / pressure) * 2f ;

        var catalyze = initBZ;

        var rate = Math.Min(1F * tempRate * pressureRate * catalyze, limit);

        var tritiumRemoved = 2f * rate;
        var nitrogenRemoved = 3f * rate * catalyze;
        var pluoxRemoved = 1f * rate;

        var nitriumProduced = 3f * rate;

        mixture.AdjustMoles(Gas.Tritium, -tritiumRemoved);
        mixture.AdjustMoles(Gas.Nitrogen, -nitrogenRemoved);
        mixture.AdjustMoles(Gas.Pluoxium, -pluoxRemoved);
        mixture.AdjustMoles(Gas.Nitrium, nitriumProduced);

        var energyReleased = rate * Atmospherics.NitriumProductionEnergy / heatScale;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * heatCap + energyReleased) / heatCap, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
