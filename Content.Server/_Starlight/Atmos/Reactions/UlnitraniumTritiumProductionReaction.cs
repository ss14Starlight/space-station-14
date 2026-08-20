using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;

/// <summary>
///     Based on Funky Atmos
///     Consumes a tiny amount of tritium to convert N2O and nitrogen to ulnitranium.
/// </summary>
[UsedImplicitly]
public sealed partial class UlnitraniumTritiumProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (mixture.Temperature > 20f && mixture.GetMoles(Gas.HyperNoblium) >= 5f)
            return ReactionResult.NoReaction;

        var initN2 = mixture.GetMoles(Gas.Nitrogen);
        var initN2O = mixture.GetMoles(Gas.NitrousOxide);
        var initTrit = mixture.GetMoles(Gas.Tritium);

        float producedAmount = Math.Min(5f, Math.Min(initN2O, Math.Min(initN2 * 2f, initTrit * 100f)));

        if (producedAmount <= 0)
            return ReactionResult.NoReaction;

        var n2oRemoved = producedAmount;
        var nitRemoved = producedAmount * 0.5f;
        var tritRemoved = producedAmount * 0.01f;
        var ulnitProduced = producedAmount;
        var hydroProduced = producedAmount * 0.01f;

        mixture.AdjustMoles(Gas.NitrousOxide, -n2oRemoved);
        mixture.AdjustMoles(Gas.Nitrogen, -nitRemoved);
        mixture.AdjustMoles(Gas.Tritium, -tritRemoved);
        mixture.AdjustMoles(Gas.Ulnitranium, ulnitProduced);
        mixture.AdjustMoles(Gas.Hydrogen, hydroProduced);

        var energyReleased = producedAmount * Atmospherics.UlnitraniumProductionEnergy;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * heatCap + energyReleased) / heatCap, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
