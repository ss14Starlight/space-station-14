using Robust.Shared.Configuration;

namespace Content.Shared._Funkystation.CCVar;

[CVarDefs]
public sealed class ReagentFireCVars
{
    /// <summary>
    /// Multiplier for the amount of fire stacks applied by flammable stains when ignited
    /// </summary>
    public static readonly CVarDef<float> StainFireStackMultiplier =
        CVarDef.Create("funkystation.reagent_fire.stain_stack_multiplier", 1.0f, CVar.SERVERONLY);

    /// <summary>
    /// Multiplier for the structural and heat damage dealt by reagent puddle fires
    /// </summary>
    public static readonly CVarDef<float> PuddleFireDamageMultiplier =
        CVarDef.Create("funkystation.reagent_fire.puddle_damage_multiplier", 1.0f, CVar.SERVERONLY);
}
