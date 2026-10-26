using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class PlasmaCombinedFireReaction : IGasReactionEffect
    {
        private const float PluoxiumFactor = 8f; // pluoxium is 8 times more effective as an oxidiser than oxygen, so make a constant for it
        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
        {
            // check for presence of oxidisers
            if (mixture.GetMoles(Gas.Oxygen) < 0.01f && mixture.GetMoles(Gas.Pluoxium) < 0.01f)
            {
                // no oxidisers, no reaction
                Console.WriteLine($"A check was performed for oxidiser this tick");
                mixture.ReactionResults[(byte)GasReaction.Fire] = 0;
                return ReactionResult.NoReaction;
            }

            var energyReleased = 0f;
            var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            var temperature = mixture.Temperature;
            var location = holder as TileAtmosphere;
            mixture.ReactionResults[(byte)GasReaction.Fire] = 0;

            // More plasma released at higher temperatures.
            var temperatureScale = 0f;

            if (temperature > Atmospherics.PlasmaUpperTemperature)
                temperatureScale = 1f;
            else
            {
                temperatureScale = (temperature - Atmospherics.PlasmaMinimumBurnTemperature) /
                                   (Atmospherics.PlasmaUpperTemperature - Atmospherics.PlasmaMinimumBurnTemperature);
            }

            if (temperatureScale > 0)
            {
                // oxygen burn rate
                var oxygenBurnRate = Atmospherics.OxygenBurnRateBase - temperatureScale;
                // pluox is 8x as effective as oxygen, so 8x less pluoxium will be burnt per mol of plasma than oxygen, thus we divide the oxygen burn rate by 8 for pluox burn rate
                var pluoxiumBurnRate = oxygenBurnRate / PluoxiumFactor;
                var plasmaBurnRate = 0f;

                var initialOxygen = mixture.GetMoles(Gas.Oxygen);
                var initialPluoxium = mixture.GetMoles(Gas.Pluoxium);
                var initialPlasmaMoles = mixture.GetMoles(Gas.Plasma);

                // as pluox is essentially 8x oxygen per mol, we can multiply the initial amount of pluox by our pluoxium factor and add that to our oxygen for the total oxidising power we have relative to oxygen
                var effectiveOxidiser = initialOxygen + (initialPluoxium * PluoxiumFactor);

                // Supersaturation makes tritium.
                // supersaturation starts at 32:1 oxidiser:plasma and reaches maximum at 96:1 oxidiser:plasma
                // we use the effective oxidiser variable to get the ratio of oxidiser to plasma relative to oxygen, controlling how much of the plasma burn is converted to tritium vs CO2
                // so instead of 1.03%-3.03% plasma to oxygen alone for supersaturation, or appx. 7.7%-20% plasma to pluox alone concentration, its now dynamic
                var oxidiserRatio = effectiveOxidiser / initialPlasmaMoles;
                var supersaturation = Math.Clamp((oxidiserRatio - Atmospherics.SuperSaturationEnds) /
                                                 (Atmospherics.SuperSaturationThreshold -
                                                  Atmospherics.SuperSaturationEnds), 0.0f, 1.0f);

                if (effectiveOxidiser > initialPlasmaMoles * Atmospherics.PlasmaOxygenFullburn)
                    plasmaBurnRate = initialPlasmaMoles * temperatureScale / Atmospherics.PlasmaBurnRateDelta;
                else
                    plasmaBurnRate = temperatureScale * (effectiveOxidiser / Atmospherics.PlasmaOxygenFullburn) / Atmospherics.PlasmaBurnRateDelta;
                if (plasmaBurnRate > Atmospherics.MinimumHeatCapacity)
                {
                    // limiting plasma consumption either to the calculated burn rate, the amount of plasma present, or the amount of avaliable pluoxium present given the burn ratio - whichever is lowest
                    plasmaBurnRate = MathF.Min(plasmaBurnRate, MathF.Min(initialPlasmaMoles, effectiveOxidiser / oxygenBurnRate));
                    mixture.SetMoles(Gas.Plasma, initialPlasmaMoles - plasmaBurnRate);
                    // total oxidiser consumed
                    var oxidiserConsumed = MathF.Min(effectiveOxidiser, plasmaBurnRate * oxygenBurnRate);
                    // pluox consumed first
                    var pluoxConsumed = MathF.Min(initialPluoxium, oxidiserConsumed / PluoxiumFactor);
                    // calculate how much effective oxidiser remains after pluoxium is consumed, if any
                    var remainingOxidiser = oxidiserConsumed - (pluoxConsumed * PluoxiumFactor);
                    // calculates how much oxygen can be consumed, preventing consumption of more than is present
                    var oxygenConsumed = MathF.Min(initialOxygen, remainingOxidiser);
                    // adjust remaining moles of gas after tick of combustion, checking if amount consumed is greater than nothing
                    if (pluoxConsumed > 0f)
                        mixture.AdjustMoles(Gas.Pluoxium, -pluoxConsumed);
                    if (oxygenConsumed > 0f)
                        mixture.AdjustMoles(Gas.Oxygen, -oxygenConsumed);

                    // supersaturation adjusts the ratio of produced tritium to unwanted CO2
                    mixture.AdjustMoles(Gas.Tritium, plasmaBurnRate * supersaturation);
                    mixture.AdjustMoles(Gas.CarbonDioxide, plasmaBurnRate * (1.0f - supersaturation));

                    energyReleased += Atmospherics.FirePlasmaEnergyReleased * plasmaBurnRate;
                    energyReleased /= heatScale; // adjust energy to make sure speedup doesn't cause mega temperature rise
                    mixture.ReactionResults[(byte)GasReaction.Fire] += plasmaBurnRate * (1 + pluoxiumBurnRate);
                }
            }

            if (energyReleased > 0)
            {
                var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
                if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                    mixture.Temperature = (temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;
            }

            if (location != null)
            {
                var mixTemperature = mixture.Temperature;
                if (mixTemperature > Atmospherics.FireMinimumTemperatureToExist)
                {
                    atmosphereSystem.HotspotExpose(location, mixTemperature, mixture.Volume);
                }
            }

            return mixture.ReactionResults[(byte)GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
        }
    }
}
