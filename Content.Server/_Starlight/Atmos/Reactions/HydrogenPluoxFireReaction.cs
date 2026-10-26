using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;

/// <summary>
///     The ignition of hydrogen in the presence of pluox at temperatures above 373.15K.
///     Copies the tritium burn reaction almost exactly, but without the radiation pulse.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class HydrogenPluoxFireReaction : IGasReactionEffect
{
    private const float PluoxiumFactor = 8f; // pluoxium is 8 times more effective than oxygen so make a constant
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (mixture.Temperature > 20f && mixture.GetMoles(Gas.HyperNoblium) >= 5f)
            return ReactionResult.NoReaction;

        var energyReleased = 0f;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var temperature = mixture.Temperature;
        var location = holder as TileAtmosphere;
        mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;
        var burnedFuel = 0f;
        var initialH2 = mixture.GetMoles(Gas.Hydrogen);
        var initialPluoxium = mixture.GetMoles(Gas.Pluoxium);
        
        var effectiveOxidiser = initialPluoxium * PluoxiumFactor; // one mol pluox will act as eight mols oxygen, so again we use the oxidising power of oxygen as a frame of reference

        if (effectiveOxidiser < initialH2 || Atmospherics.MinimumHydrogenOxyburnEnergy > (temperature * oldHeatCapacity * heatScale))
        {
            burnedFuel = effectiveOxidiser / Atmospherics.HydrogenBurnOxyFactor;
            if (burnedFuel > initialH2)
                burnedFuel = initialH2;
        }
        else
        {
            burnedFuel = Math.Min(initialH2, effectiveOxidiser / Atmospherics.TritiumBurnFuelRatio) / Atmospherics.TritiumBurnTritFactor;
        }

        if (burnedFuel <= 0f)
            return ReactionResult.NoReaction;

        // convert the effective oxidiser variable back into the actual amount of pluox gas to consume
        var pluoxiumConsumed = burnedFuel / Atmospherics.TritiumBurnFuelRatio / PluoxiumFactor; 
        if (initialH2 - burnedFuel < 0f || initialPluoxium - pluoxiumConsumed < 0f)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Hydrogen, -burnedFuel);
        mixture.AdjustMoles(Gas.Pluoxium, -pluoxiumConsumed);

        if (burnedFuel > 0)
        {
            energyReleased += (Atmospherics.FireHydrogenEnergyReleased * burnedFuel);

            // Conservation of mass is important.
            mixture.AdjustMoles(Gas.WaterVapor, burnedFuel);

            mixture.ReactionResults[(byte)GasReaction.Fire] += burnedFuel;
        }

        energyReleased /= heatScale; // adjust energy to make sure speedup doesn't cause mega temperature rise
        if (energyReleased > 0)
        {
            var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity) mixture.Temperature = ((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity);
        }

        if (location != null)
        {
            temperature = mixture.Temperature;
            if (temperature > Atmospherics.FireMinimumTemperatureToExist)
            {
                atmosphereSystem.HotspotExpose(location, temperature, mixture.Volume);
            }
        }

        return mixture.ReactionResults[(byte)GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
    }
}
