using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;

    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class TritiumCombinedFireReaction : IGasReactionEffect
    {
        private const float PluoxiumFactor = 8f; // pluoxium is 8 times more effective than oxygen so make a constant
        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
        {
            // check for presence of oxidisers as the yaml only checks for fuel
            if (mixture.GetMoles(Gas.Oxygen) < 0.01f && mixture.GetMoles(Gas.Pluoxium) < 0.01f)
            {
                // no oxidisers, no reaction
                mixture.ReactionResults[(byte)GasReaction.Fire] = 0;
                return ReactionResult.NoReaction;
            }

            var energyReleased = 0f;
            var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            var temperature = mixture.Temperature;
            mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;
            var burnedFuel = 0f;
            var initialTrit = mixture.GetMoles(Gas.Tritium);
            var initialOxygen = mixture.GetMoles(Gas.Oxygen);
            var initialPluoxium = mixture.GetMoles(Gas.Pluoxium);

            // sum all present oxidisers together to get the amount of effective oxidiser we have relative to one mole of oxygen
            var effectiveOxidiser = initialOxygen + (initialPluoxium * PluoxiumFactor);

            if (effectiveOxidiser < initialTrit ||
                Atmospherics.MinimumTritiumOxyburnEnergy > (temperature * oldHeatCapacity * heatScale))
            {
                // effective oxidiser divided by 100f = amount of tritium that can be burned given the amount of oxidiser present
                // take smallest of either trit present or effective oxidiser burned
                burnedFuel = MathF.Min(initialTrit, effectiveOxidiser / Atmospherics.TritiumBurnOxyFactor);
                // find out how much oxidiser is being consumed, and make sure we don't consume more than is available
                var oxidiserConsumed = burnedFuel / Atmospherics.TritiumBurnFuelRatio;
                // work out how much pluox is being consumed, then remove it first, making sure it is a non-negative number
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
                // find out how much oxidiser is being consumed, and make sure we don't consume more than is available
                var oxidiserConsumed = burnedFuel / Atmospherics.TritiumBurnFuelRatio;
                // work out how much pluox is being consumed, then remove it first, making sure it is a non-negative number
                var pluoxConsumed = MathF.Min(initialPluoxium, oxidiserConsumed / PluoxiumFactor);
                // how much oxidising power we have left
                var remainingOxidiser = oxidiserConsumed - (pluoxConsumed * PluoxiumFactor);
                // oxy consumed
                var oxygenConsumed = MathF.Min(initialOxygen, remainingOxidiser);
                // adjust gas mixture by mols consumed - only if it is greater than nothing
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
                    mixture.Temperature = ((temperature * oldHeatCapacity) + energyReleased) / newHeatCapacity;
            }

            if (holder is TileAtmosphere location)
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
