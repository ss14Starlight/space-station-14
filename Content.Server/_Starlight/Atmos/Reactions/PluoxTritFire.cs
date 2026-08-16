using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class PluoxTritFire : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (mixture.Temperature > 20f && mixture.GetMoles(Gas.HyperNoblium) >= 5f)
            return ReactionResult.NoReaction;
///Get ingredients

        var initialPluoxMoles = mixture.GetMoles(Gas.Pluoxium);
        var initialCo2moles = mixture.GetMoles(Gas.CarbonDioxide);
        var initialOxygenmoles = mixture.GetMoles(Gas.Oxygen);
        var initialTritiumMoles = mixture.GetMoles(Gas.Tritium);
        var temperature = mixture.Temperature;

///Check pluox flat concentration relative to fuel, Co2 contribuites to reduce threshold for intentional reaction. Oxy makes reaction harder to reduce accidental fires.

        var pluoxSat = (0f);
        if (initialPluoxMoles > 1)
        {
            pluoxSat = ((initialPluoxMoles + (initialCo2moles * 0.25f)) - (initialOxygenmoles * 0.25f));
        }
        var pluoxRatio = (pluoxSat / initialTritiumMoles);

///Too much pluox? Ignite! It's super oxygen and doesnt care about current temperature.

        var satrate = (0f);
        var temprate = (0f);

        if (pluoxSat > 25)
        {
        satrate = (pluoxRatio * 0.01f);
        }

///Can also ignite from very high temperatures.

        if (temperature > 1500f)
        {
        temprate = (temperature/1500f);
        }

        var rate = (satrate + temprate);
        var burn = (0f);

        if (rate < 0.01f)
            return ReactionResult.NoReaction;

///At a certain rate the reaction gets slower unless cooled to prevent insanity.

        if (rate > 1f)
            burn = (1f + (rate / temperature));
        else
        {
            burn = (rate);
        }

///Don't burn fuel that doesnt exist.

        burn = Math.Min(burn, Math.Min(initialTritiumMoles, initialPluoxMoles * 2f));

        mixture.AdjustMoles(Gas.Tritium, -burn);
        mixture.AdjustMoles(Gas.Pluoxium, -burn * 0.5f);
        mixture.AdjustMoles(Gas.WaterVapor, burn);

        var energyReleased = (Atmospherics.FireHydrogenEnergyReleased * burn);

///While generic conversion interactions make sense to me, the exact mechanics of fire and fire visuals, do not.

        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * heatCap + energyReleased) / heatCap, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
