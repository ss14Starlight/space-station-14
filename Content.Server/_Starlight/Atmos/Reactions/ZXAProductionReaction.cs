using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;

/// <summary>
///     Based on Funky Atmos
///     Forms ZXA from mixing BZ and Nitrous Oxide at low pressure. Also decomposes Nitrous Oxide when there are more than 3 parts BZ per N2O.
/// </summary>
[UsedImplicitly]
public sealed partial class ZXAProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (mixture.Temperature > 20f && mixture.GetMoles(Gas.HyperNoblium) >= 5f)
            return ReactionResult.NoReaction;

        var initN2O = mixture.GetMoles(Gas.NitrousOxide);
        var initBZ = mixture.GetMoles(Gas.BZ);
        var initVapor = mixture.GetMoles(Gas.WaterVapor);
        var pressure = mixture.Pressure;
        var volume = mixture.Volume;

        var environmentEfficiency = volume / pressure; // more volume and less pressure gives better rates
        var ratioEfficiency = Math.Min(initN2O / initBZ, 1f); // less n2o than BZ gives lower rates
        var zxaFormed = Math.Min(0.02f * ratioEfficiency * environmentEfficiency, Math.Min(initN2O * 2.25f, Math.Min(initBZ * 1f, initVapor * 1.5f)));

        var nitrousOxideDecomposed = Math.Max(4f * (initBZ / (initN2O + initBZ) - 0.75f), 0);
        var nitrogenAdded = 0f;
        var oxygenAdded = 0f;
        if (nitrousOxideDecomposed > 0)
        {
            var amountDecomposed = 0.4f * zxaFormed * nitrousOxideDecomposed;
            nitrogenAdded = amountDecomposed;
            oxygenAdded = 0.5f * amountDecomposed;
        }
        var zxaAdded = zxaFormed * (1f - nitrousOxideDecomposed);
        var n2oRemoved = zxaFormed / 2.25f;
        var bzRemoved = zxaFormed * (1f - nitrousOxideDecomposed);
        var vaporRemoved = zxaFormed / 3f; // Half of the water vapor is returned, it's a semi-catalyst.

        if (n2oRemoved > initN2O || bzRemoved > initBZ|| vaporRemoved > initVapor)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.NitrousOxide, -n2oRemoved);
        mixture.AdjustMoles(Gas.BZ, -bzRemoved);
        mixture.AdjustMoles(Gas.WaterVapor, -vaporRemoved);
        mixture.AdjustMoles(Gas.Nitrogen, nitrogenAdded);
        mixture.AdjustMoles(Gas.Oxygen, oxygenAdded);
        mixture.AdjustMoles(Gas.ZXA, zxaAdded);

        var energyReleased = zxaFormed * (Atmospherics.BZProductionEnergy + nitrousOxideDecomposed);
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * heatCap + energyReleased) / heatCap, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
