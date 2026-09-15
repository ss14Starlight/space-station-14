using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Order value multiplier for the reward given when a tamper seal is opened.
    /// </summary>
    public static readonly CVarDef<float> TamperSealRewardMultiplier =
        CVarDef.Create("cargo.tamper_seal_reward_mult", 0.1f, CVar.SERVER);

    /// <summary>
    /// Order value multiplier for the penalty applied when a tamper seal is destroyed.
    /// This is purely deducted from the deliverer.
    /// </summary>
    public static readonly CVarDef<float> TamperSealPenaltyMultiplier =
        CVarDef.Create("cargo.tamper_seal_penalty_mult", 0.1f, CVar.SERVER);

    /// <summary>
    /// Order value multiplier for the refund given to the recipient party when a tamper seal is destroyed.
    /// This is deducted from the deliverer and given to the recipient.
    /// </summary>
    public static readonly CVarDef<float> TamperSealRefundMultiplier =
        CVarDef.Create("cargo.tamper_seal_refund_mult", 0.5f, CVar.SERVER);
}
