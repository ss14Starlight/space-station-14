using Content.Shared.Atmos;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;

/// <summary>
/// Shared functions for both the forward and reverse reactions of ammonia synthesis.
/// </summary>

[UsedImplicitly]
public sealed partial class AmmoniaSynthesisShared
{
    public static float CalculateKp(float temperature)
    {
        var ln_kp = (Atmospherics.AmmoniaNegativeDeltaEnthalpyOverR * (1.0f / temperature)) + Atmospherics.AmmoniaDeltaEntropyOverR;
        return float.Exp(ln_kp);
    }

    public static float CalculateQp(float pAmmonia, float pNitrogen, float pHydrogen)
    {
        if (pAmmonia <= 0.00001f)
            return 0.0f;

        var numerator = float.Pow(pAmmonia, 2);
        var denominator = float.Pow(pHydrogen, 3) * pNitrogen;

        if (denominator <= 0.0f)
            return 1e6f;

        return  numerator / denominator;
    }
}
