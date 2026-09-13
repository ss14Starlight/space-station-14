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
public sealed partial class AmmoniaSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (mixture.GetMoles(Gas.HyperNoblium) >= 5.0f)
            return ReactionResult.NoReaction;

        var nAmmonia = mixture.GetMoles(Gas.Ammonia);
        var nNitrogen = mixture.GetMoles(Gas.Nitrogen);
        var nHydrogen = mixture.GetMoles(Gas.Hydrogen);

        // Obtain partial pressures of each gas
        var pAmmonia = nAmmonia * Atmospherics.R * mixture.Temperature / mixture.Volume / Atmospherics.OneAtmosphere;
        var pNitrogen = nNitrogen * Atmospherics.R * mixture.Temperature / mixture.Volume / Atmospherics.OneAtmosphere;
        var pHydrogen = nHydrogen * Atmospherics.R * mixture.Temperature / mixture.Volume / Atmospherics.OneAtmosphere;

        // Equilibrium Constant at a given temperature
        var kp = CalculateKp(mixture.Temperature);
        // Equilibrium Quotient at the given partial concentrations
        var qp = CalculateQp(pAmmonia, pNitrogen, pHydrogen);

        var deltaMoles = 0f;
        var energyReleased = 0f;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (qp < kp)
        {
            if (nHydrogen < 0.01 || nNitrogen < 0.01)
                return ReactionResult.NoReaction;

            // Forward reaction rate increases as temp increases
            var forwardTempReactionRate = 0.4f * (1 - float.Exp(-0.00005f * mixture.Temperature));
            // The forward reaction rate also increases as the partial pressures of its components increases
            var reactionRate = forwardTempReactionRate * float.Pow(pNitrogen, 0.2f) * float.Pow(pHydrogen, 0.2f) * (1.0f - (qp / kp));

            deltaMoles = reactionRate * mixture.Volume;
            deltaMoles *= 0.001f;

            deltaMoles = Math.Min(deltaMoles, nNitrogen);
            deltaMoles = Math.Min(deltaMoles, nHydrogen / 3.0f);
        }
        else if (qp > kp)
        {
            if (nAmmonia < 0.01)
                return ReactionResult.NoReaction;

            // Reverse reaction rate increases as heat increases at a faster rate than the forward reaction because
            // it is an endothermic reaction
            var reverseTempReactionRate = 0.6f * (1 - float.Exp(-0.0001f * mixture.Temperature));
            // More moles of ammonia means more decaying
            var reactionRate = reverseTempReactionRate * float.Pow(pAmmonia, 0.2f) * (1.0f - (kp / qp));

            deltaMoles = reactionRate * mixture.Volume;
            deltaMoles *= 0.001f;
            deltaMoles = -Math.Min(deltaMoles, nAmmonia / 2.0f);
        }
        else
        {
            return ReactionResult.NoReaction;
        }

        // Positive deltaMoles for forward reaction, negative deltaMoles for reverse reaction

        if (float.Abs(deltaMoles) <= 0.00001f)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Ammonia, deltaMoles * 2);
        mixture.AdjustMoles(Gas.Hydrogen, -deltaMoles * 3);
        mixture.AdjustMoles(Gas.Nitrogen, -deltaMoles);

        energyReleased = Atmospherics.AmmoniaProductionEnergyReleased * deltaMoles * 2.0f;

        energyReleased /= heatScale;
        if (float.Abs(energyReleased) > 0)
        {
            var temperature = mixture.Temperature;

            var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = ((temperature * oldHeatCapacity) + energyReleased) / newHeatCapacity;
        }

        return ReactionResult.Reacting;
    }

    private static float CalculateKp(float temperature)
    {
        var ln_kp = (Atmospherics.AmmoniaNegativeDeltaEnthalpyOverR * (1.0f / temperature)) + Atmospherics.AmmoniaDeltaEntropyOverR;
        return float.Exp(ln_kp);
    }

    /// <summary>
    /// Calculates the equilibrium quotient for the ammonia synthesis reaction
    /// </summary>
    private static float CalculateQp(float pAmmonia, float pNitrogen, float pHydrogen)
    {
        if (pAmmonia <= 0.00001f)
            return 0.0f;

        var numerator = float.Pow(pAmmonia, 2);
        var denominator = float.Pow(pHydrogen, 3) * pNitrogen;

        if (denominator <= 0.0f)
            return 1e6f;

        return numerator / denominator;
    }
}
