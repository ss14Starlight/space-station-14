using Content.Shared._FarHorizons.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._FarHorizons.Power.Generation.FissionGenerator;

public sealed partial class ReactorPartSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public float ReactionRate { get; private set; }
    public float NeutronReactionBias { get; private set; }
    public float ReactionReactant { get; private set; }
    public float ReactionProduct { get; private set; }
    public float StimulatedHeatingFactor { get; private set; }
    public float SpontaneousHeatingFactor { get; private set; }
    public float SpontaneousReactionConsumptionMultiplier { get; private set; }
    public float ReactorPartHotTemp { get; private set; }
    public float ReactorPartBurnTemp { get; private set; }

    /// <summary>
    /// Processing multiplier based on atmospherics time and speedup cvar
    /// </summary>
    public float ProcMult => _atmosphereSystem.AtmosTime * _atmosphereSystem.Speedup * 6; // The 6 is a magic number to make things work at a reasonable rate

    /// <summary>
    /// Ratio of product to reactant for reactions
    /// </summary>
    public float ReactionRatio => ReactionProduct / ReactionReactant;

    private void InitializeCVars()
    {
        Subs.CVar(_cfg, FHCCVars.ReactionRate, value => ReactionRate = value, true);
        Subs.CVar(_cfg, FHCCVars.NeutronReactionBias, value => NeutronReactionBias = value, true);
        Subs.CVar(_cfg, FHCCVars.ReactionReactant, value => ReactionReactant = value, true);
        Subs.CVar(_cfg, FHCCVars.ReactionProduct, value => ReactionProduct = value, true);
        Subs.CVar(_cfg, FHCCVars.StimulatedHeatingFactor, value => StimulatedHeatingFactor = value, true);
        Subs.CVar(_cfg, FHCCVars.SpontaneousHeatingFactor, value => SpontaneousHeatingFactor = value, true);
        Subs.CVar(_cfg, FHCCVars.SpontaneousReactionConsumptionMultiplier, value => SpontaneousReactionConsumptionMultiplier = value, true);
        Subs.CVar(_cfg, FHCCVars.ReactorPartHotTemp, value => ReactorPartHotTemp = value, true);
        Subs.CVar(_cfg, FHCCVars.ReactorPartBurnTemp, value => ReactorPartBurnTemp = value, true);
    }
}