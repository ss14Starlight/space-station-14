using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class PlasmaPluoxFireReaction : IGasReactionEffect
    {
        private const float PluoxiumFactor = 8f; // pluoxium is 8 times more effective as an oxidiser than oxygen, so make a constant for it
        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
        {
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
                // pluox is 8x as effective as oxygen, so the same amount of pluoxium will burn 8x slower than oxygen, thus we divide the oxygen burn rate by 8 for pluox burn rate
                var pluoxiumBurnRate = (Atmospherics.OxygenBurnRateBase - temperatureScale) / PluoxiumFactor;
                var plasmaBurnRate = 0f;

                var initialPluoxium = mixture.GetMoles(Gas.Pluoxium);
                var initialPlasmaMoles = mixture.GetMoles(Gas.Plasma);

                // as pluox is essentially 8x oxygen per mol, we can multiply the initial amount of pluox by our pluoxium factor to get the amount of effective oxidiser we have
                var effectiveOxidiser = initialPluoxium * PluoxiumFactor;

                // Supersaturation makes tritium.
                // instead of 1.03% to 3.03% plasma to oxygen for supersaturation, its now appx. 7.7% to 20% plasma concentration for tritium production, as pluox is 8x the oxidiser that oxygen is
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
                    // limiting the amount of plasma burned by either our initial plasma or the amount of pluoxium available given the burn ratio.
                    plasmaBurnRate = MathF.Min(plasmaBurnRate, MathF.Min(initialPlasmaMoles, initialPluoxium / pluoxiumBurnRate));
                    mixture.SetMoles(Gas.Plasma, initialPlasmaMoles - plasmaBurnRate);
                    mixture.SetMoles(Gas.Pluoxium, initialPluoxium - plasmaBurnRate * pluoxiumBurnRate);

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