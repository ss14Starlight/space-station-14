using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;


// Pretty much just a copy of the normal version, but using Ulnitranium instead of Pluoxium. Proto Nitrate is still a useless gas, but hey, more ways to make it, I guess.
[UsedImplicitly]
public sealed partial class ProtoNitrateAlternateProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (mixture.Temperature > 20f && mixture.GetMoles(Gas.HyperNoblium) >= 5f)
            return ReactionResult.NoReaction;

        var initUlnit = mixture.GetMoles(Gas.Ulnitranium);
        var initH2 = mixture.GetMoles(Gas.Hydrogen);

        var temperature = mixture.Temperature;
        var heatEfficiency = Math.Min(temperature * 0.005f, Math.Min(initUlnit * 5f, initH2 * 0.5f));

        if (heatEfficiency <= 0 || initUlnit - heatEfficiency * 0.2f < 0 || initH2 - heatEfficiency * 2f < 0)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Hydrogen, -heatEfficiency * 2f);
        mixture.AdjustMoles(Gas.Ulnitranium, -heatEfficiency * 0.2f);
        mixture.AdjustMoles(Gas.ProtoNitrate, heatEfficiency * 2.2f);

        var energyReleased = heatEfficiency * Atmospherics.ProtoNitrateProductionEnergy;

        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * heatCap + energyReleased) / heatCap, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
