using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class PluoxPlasmaFire : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (mixture.Temperature > 20f && mixture.GetMoles(Gas.HyperNoblium) >= 5f)
            return ReactionResult.NoReaction;
///Get ingredients
		
		var initialPluoxMoles = mixture.GetMoles(Gas.Pluoxium);
		var initialPlasmaMoles = mixture.GetMoles(Gas.Plasma);
		
///Check pluox flat concentration relative to fuel 

		var pluoxRatio = initialPluoxMoles / initialPlasmaMoles;		

///Too much pluox? Ignite! It's super oxygen and doesnt care about current temperature.

        if (initialPluoxMoles < 50f)
            return ReactionResult.NoReaction;

		var satrate = (0f);
		var temprate = (0f);

		if (pluoxRatio > 2f)
		{
			satrate = (pluoxRatio * 0.1f);
		}

///Can also ignite from very high temperatures.

		if (temperature > 1500f)
		{
			temprate = (temperature/1500f);
		}

        var rate = (satrate + temprate);

        if (rate < 1f)
            return ReactionResult.NoReaction; 
				
		mixture.AdjustMoles(Gas.Plasma, -rate);
		mixture.AdjustMoles(Gas.Pluoxium, -rate);
		mixture.AdjustMoles(Gas.Tritium, rate);
			
		var energyReleased = (Atmospherics.FirePlasmaEnergyReleased * rate);
		
///While generic conversion interactions make sense to me, the exact mechanics of fire and fire visuals, do not.		

        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * heatCap + energyReleased) / heatCap, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
