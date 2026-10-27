using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class TritiumCombinedFireReaction : IGasReactionEffect
    {
        private const float PluoxiumFactor = 8f; // pluoxium is 8 times more effective than oxygen so make a constant
        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
        {
            // check for presence of oxidisers
            if (mixture.GetMoles(Gas.Oxygen) < 0.01f && mixture.GetMoles(Gas.Pluoxium) < 0.01f)
            {
                // no oxidisers, no reaction
                mixture.ReactionResults[(byte)GasReaction.Fire] = 0;
                return ReactionResult.NoReaction;
            }

            var energyReleased = 0f;
            var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            var temperature = mixture.Temperature;
            var location = holder as TileAtmosphere;
            mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;
            var burnedFuel = 0f;
            var initialTrit = mixture.GetMoles(Gas.Tritium);
            var initialOxygen = mixture.GetMoles(Gas.Oxygen);
            var initialPluoxium = mixture.GetMoles(Gas.Pluoxium);

            // one mol pluox acts as eight mols oxygen as an oxidiser, so we multiply the initial amount of pluox by 8 and add that to the inital amount of oxygen to get the effective amount of oxidiser we have
            var effectiveOxidiser = initialOxygen + (initialPluoxium * PluoxiumFactor);

            if (effectiveOxidiser < initialTrit ||
                Atmospherics.MinimumTritiumOxyburnEnergy > (temperature * oldHeatCapacity * heatScale))
            {
                // effective oxidiser divided by 100f = amount of tritium that can be burned given the amount of oxidiser present
                // take smallest of either trit present or effective oxidiser burned
                burnedFuel = MathF.Min(initialTrit, effectiveOxidiser / Atmospherics.TritiumBurnOxyFactor);
                // burned fuel / trit burn ratio = the amount of oxidiser consumed
                var oxidiserConsumed = burnedFuel / Atmospherics.TritiumBurnFuelRatio;
                // pluox is consumed first, and is the smaller of either the amount of pluox present, or total oxidiser consumed divided by 8
                var pluoxConsumed = MathF.Min(initialPluoxium, oxidiserConsumed / PluoxiumFactor);
                // how much oxidising power we have left
                var remainingOxidiser = oxidiserConsumed - (pluoxConsumed * PluoxiumFactor);
                // how much oxygen can be consumed, without consuming more than is available
                var oxygenConsumed = MathF.Min(initialOxygen, remainingOxidiser);
                // adjust gas mixture by mols consumed - only if it is greater than nothing
                if (pluoxConsumed > 0f)
                    mixture.AdjustMoles(Gas.Pluoxium, -pluoxConsumed);
                if (oxygenConsumed > 0f)
                    mixture.AdjustMoles(Gas.Oxygen, -oxygenConsumed);
                mixture.AdjustMoles(Gas.Tritium, -burnedFuel);
            }
            else
            {
                // Limit the amount of fuel burned by the limiting reactant, either our initial tritium or the amount of oxidiser available given the burn ratio.
                burnedFuel = Math.Min(initialTrit, effectiveOxidiser / Atmospherics.TritiumBurnFuelRatio) / Atmospherics.TritiumBurnTritFactor;
                // burned fuel / trit burn ratio = amount of effective oxidiser consumed
                var oxidiserConsumed = burnedFuel / Atmospherics.TritiumBurnFuelRatio;
                // convert the effective oxidiser variable back into the amount of pluox to consume first
                var pluoxConsumed = MathF.Min(initialPluoxium, oxidiserConsumed / PluoxiumFactor);
                // remaining oxidiser
                var remainingOxidiser = oxidiserConsumed - (pluoxConsumed * PluoxiumFactor);
                // oxy consumed
                var oxygenConsumed = MathF.Min(initialOxygen, remainingOxidiser);
                // adjust remaining mols
                if (pluoxConsumed > 0f)
                    mixture.AdjustMoles(Gas.Pluoxium, -pluoxConsumed);
                if (oxygenConsumed > 0f)
                    mixture.AdjustMoles(Gas.Oxygen, -oxygenConsumed);
                mixture.AdjustMoles(Gas.Tritium, -burnedFuel);
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
