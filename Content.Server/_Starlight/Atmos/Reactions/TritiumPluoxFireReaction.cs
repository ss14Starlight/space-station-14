using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class TritiumPluoxFireReaction : IGasReactionEffect
    {
        private const float PluoxiumFactor = 8f; // pluoxium is 8 times more effective than oxygen so make a constant
        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
        {
            var energyReleased = 0f;
            var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            var temperature = mixture.Temperature;
            var location = holder as TileAtmosphere;
            mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;
            var burnedFuel = 0f;
            var initialTrit = mixture.GetMoles(Gas.Tritium);
            var initialPluoxium = mixture.GetMoles(Gas.Pluoxium);

            // one mol pluox acts as eight mols oxygen as an oxidiser, so we multiply the initial amount of pluox by 8 to get the effective amount of oxidiser relative to oxygen 
            var effectiveOxidiser = initialPluoxium * PluoxiumFactor;

            if (effectiveOxidiser < initialTrit ||
                Atmospherics.MinimumTritiumOxyburnEnergy > (temperature * oldHeatCapacity * heatScale))
            {
                // effective oxidiser divided by 100f = amount of tritium that can be burned given the amount of oxidiser present
                burnedFuel = effectiveOxidiser / Atmospherics.TritiumBurnOxyFactor;
                if (burnedFuel > initialTrit)
                    burnedFuel = initialTrit;

                // burned fuel / trit burn ratio = the amount of oxidiser consumed, which we then divide by our pluoxium factor to get the actual amount of pluox consumed
                var pluoxiumConsumed = burnedFuel / Atmospherics.TritiumBurnFuelRatio / PluoxiumFactor;
                if (pluoxiumConsumed > initialPluoxium)
                    pluoxiumConsumed = initialPluoxium;
                
                mixture.AdjustMoles(Gas.Tritium, -burnedFuel);
                mixture.AdjustMoles(Gas.Pluoxium, -pluoxiumConsumed);
            }
            else
            {
                // Limit the amount of fuel burned by the limiting reactant, either our initial tritium or the amount of pluoxium available given the burn ratio.
                burnedFuel = Math.Min(initialTrit, effectiveOxidiser / Atmospherics.TritiumBurnFuelRatio) / Atmospherics.TritiumBurnTritFactor;
                // convert the effective oxidiser variable back into the actual amount of pluox to consume again
                var pluoxiumConsumed = burnedFuel / Atmospherics.TritiumBurnFuelRatio / PluoxiumFactor;
                if (pluoxiumConsumed > initialPluoxium)
                    pluoxiumConsumed = initialPluoxium;

                mixture.AdjustMoles(Gas.Tritium, -burnedFuel);
                mixture.AdjustMoles(Gas.Pluoxium, -pluoxiumConsumed);
                energyReleased += (Atmospherics.FireHydrogenEnergyReleased * burnedFuel * (Atmospherics.TritiumBurnTritFactor - 1));
            }

            if (burnedFuel > 0)
            {
                energyReleased += (Atmospherics.FireHydrogenEnergyReleased * burnedFuel);

                // TODO ATMOS Radiation pulse here!

                // Conservation of mass is important.
                mixture.AdjustMoles(Gas.WaterVapor, burnedFuel);

                mixture.ReactionResults[(byte)GasReaction.Fire] += burnedFuel;
            }

            energyReleased /= heatScale; // adjust energy to make sure speedup doesn't cause mega temperature rise
            if (energyReleased > 0)
            {
                var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
                if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                    mixture.Temperature = ((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity);
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
}