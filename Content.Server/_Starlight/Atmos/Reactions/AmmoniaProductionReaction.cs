using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;

/// <summary>
///     Produces ammonia from hydrogen and nitrogen. It is a reversible reaction that occurs at the same time as
///     the ammonia decay.
/// </summary>
[UsedImplicitly]
public sealed partial class AmmoniaProductionReaction : IGasReactionEffect
{
    private const float NegDeltaHOverR = 11089.4f;
    private const float DeltaSOverR = -20.838f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var kp = CalculateKp(mixture.Temperature);

        var nAmmonia = mixture.GetMoles(Gas.Ammonia);
        var nNitrogen = mixture.GetMoles(Gas.Nitrogen);
        var nHydrogen = mixture.GetMoles(Gas.Hydrogen);

        var pAmmonia = nAmmonia * Atmospherics.R * mixture.Temperature / mixture.Volume / Atmospherics.OneAtmosphere;
        var pNitrogen = nNitrogen * Atmospherics.R * mixture.Temperature / mixture.Volume / Atmospherics.OneAtmosphere;
        var pHydrogen = nHydrogen * Atmospherics.R * mixture.Temperature / mixture.Volume / Atmospherics.OneAtmosphere;

        var qp = CalculateQp(pAmmonia, pNitrogen, pHydrogen);

        if (qp >= kp)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        var forwardTempReactionRate = 0.1f*(1 - float.Pow(float.E, -0.00005f * mixture.Temperature));
        var reactionRate = forwardTempReactionRate * float.Pow(pNitrogen, 0.2f) * float.Pow(pHydrogen, 0.2f) * (1.0f - (qp / kp));

        var deltaMoles = reactionRate * mixture.Volume;
        deltaMoles *= 0.01f;

        deltaMoles = Math.Min(deltaMoles, nNitrogen);
        deltaMoles = Math.Min(deltaMoles, nHydrogen / 3.0f);

        if (deltaMoles <= 0.00001f)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Ammonia, deltaMoles * 2);
        mixture.AdjustMoles(Gas.Hydrogen, -deltaMoles * 3);
        mixture.AdjustMoles(Gas.Nitrogen, -deltaMoles);

        var energyReleased = Atmospherics.AmmoniaProductionEnergyReleased * deltaMoles / 2.0f;

        energyReleased /= heatScale;
        if (energyReleased > 0)
        {
            var temperature = mixture.Temperature;

            var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = ((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity);
        }

        return ReactionResult.Reacting;

    }

    private static float CalculateKp(float temperature)
    {
        var ln_kp = NegDeltaHOverR * (1.0f / temperature) + DeltaSOverR;
        return float.Exp(ln_kp);
    }

    private static float CalculateQp(float pAmmonia, float pNitrogen, float pHydrogen)
    {
        if (pAmmonia <= 0.00001f)
            return 0.0f;

        var numerator = float.Pow(pAmmonia, 2);
        var denominator = float.Pow(pHydrogen, 3) * pNitrogen;

        if (denominator <= 0.0f)
            return 1e6f;

        return denominator <= 0.0f ? 0.0f : numerator / denominator;
    }
}
