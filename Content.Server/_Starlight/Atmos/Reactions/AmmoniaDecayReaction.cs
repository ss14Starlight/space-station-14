using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;

/// <summary>
///     The reverse reaction of ammonia production. Ammonia will decay at high temperatures back into its
///     component gases. This reaction has a rate of reaction that increases with a higher temp at a
///     faster rate than the forward reaction, as a result, it is intended that a higher temp yields more
///     decay but at a faster rate.
/// </summary>
[UsedImplicitly]
public sealed partial class AmmoniaDecayReaction : IGasReactionEffect
{
    private const float NegDeltaHOverR = 11089.4f;
    private const float DeltaSOverR = -20.838f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var kp = AmmoniaSynthesisShared.CalculateKp(mixture.Temperature);

        var nAmmonia = mixture.GetMoles(Gas.Ammonia);
        var nNitrogen = mixture.GetMoles(Gas.Nitrogen);
        var nHydrogen = mixture.GetMoles(Gas.Hydrogen);

        var pAmmonia = nAmmonia * Atmospherics.R * mixture.Temperature / mixture.Volume / Atmospherics.OneAtmosphere;
        var pNitrogen = nNitrogen * Atmospherics.R * mixture.Temperature / mixture.Volume / Atmospherics.OneAtmosphere;
        var pHydrogen = nHydrogen * Atmospherics.R * mixture.Temperature / mixture.Volume / Atmospherics.OneAtmosphere;

        var qp = AmmoniaSynthesisShared.CalculateQp(pAmmonia, pNitrogen, pHydrogen);

        if (qp <= kp)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        var reverseTempReactionRate = 0.1f*(1 - float.Exp(-0.0001f * mixture.Temperature));
        var reactionRate = reverseTempReactionRate * float.Pow(pAmmonia, 0.2f) * (1.0f - (kp / qp));

        var deltaMoles = reactionRate * mixture.Volume;
        deltaMoles *= 0.01f;
        deltaMoles = Math.Min(deltaMoles, nAmmonia / 2.0f);

        if (deltaMoles <= 0.00001f)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Ammonia, -deltaMoles * 2);
        mixture.AdjustMoles(Gas.Hydrogen, deltaMoles * 3);
        mixture.AdjustMoles(Gas.Nitrogen, deltaMoles);

        var energyReleased = Atmospherics.AmmoniaProductionEnergyReleased * deltaMoles * 2.0f;

        energyReleased /= heatScale;
        if (energyReleased > 0)
        {
            var temperature = mixture.Temperature;

            var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = ((temperature * oldHeatCapacity) - energyReleased) / newHeatCapacity;
        }

        return ReactionResult.Reacting;
    }
}
