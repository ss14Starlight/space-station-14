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

    /// <summary>
    /// Defines whether footprints are flammable.
    /// </summary>
    public static readonly CVarDef<bool> FootprintsFlammable =
        CVarDef.Create("funkystation.reagent_fire.footprints_flammable", true, CVar.SERVERONLY);

    /// <summary>
    /// Multiplier for effectiveness of fire protection from equipment.
    /// </summary>
    public static readonly CVarDef<float> FireProtectionEffectiveness =
        CVarDef.Create("funkystation.reagent_fire.fire_protection_effectiveness", 1.0f, CVar.SERVERONLY);

    /// <summary>
    /// Whether puddle volume scales down effective fire intensity for small amounts of liquid
    /// </summary>
    public static readonly CVarDef<bool> VolumeScalingEnabled =
        CVarDef.Create("funkystation.reagent_fire.volume_scaling_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// The solution volume in units at which fire intensity is considered full strength. Puddles below this volume burn proportionally weaker
    /// </summary>
    public static readonly CVarDef<float> VolumeScalingReference =
        CVarDef.Create("funkystation.reagent_fire.volume_scaling_reference", 20f, CVar.SERVERONLY);

    /// <summary>
    /// Exponent applied to the volume ratio. Higher values punish small puddles harder. 1.0 is linear falloff.
    /// </summary>
    public static readonly CVarDef<float> VolumeScalingCurve =
        CVarDef.Create("funkystation.reagent_fire.volume_scaling_curve", 1.5f, CVar.SERVERONLY);

    /// <summary>
    /// Solution volume in units below which puddles burn out rapidly instead of following the normal rate
    /// </summary>
    public static readonly CVarDef<float> SmallPuddleBurnThreshold =
        CVarDef.Create("funkystation.reagent_fire.small_puddle_burn_threshold", 5.0f, CVar.SERVERONLY);

    /// <summary>
    /// Percentage of remaining volume consumed per second once a puddle is below the above threshold
    /// </summary>
    public static readonly CVarDef<float> SmallPuddleBurnPercent =
        CVarDef.Create("funkystation.reagent_fire.small_puddle_burn_percent", 0.5f, CVar.SERVERONLY);
}
