using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<float> VirologyContaminationSampleInterval =
        CVarDef.Create("virology.contamination_sample_interval", 10f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationRottingCorpse =
        CVarDef.Create("virology.contamination_rotting_corpse", 3f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationRottenFood =
        CVarDef.Create("virology.contamination_rotten_food", 1.5f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationOtherRot =
        CVarDef.Create("virology.contamination_other_rot", 0.9f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationBiologicalPuddlePerUnit =
        CVarDef.Create("virology.contamination_biological_puddle_per_unit", 0.06f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationBiologicalPuddleMaximum =
        CVarDef.Create("virology.contamination_biological_puddle_maximum", 2.4f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationFoodPuddlePerUnit =
        CVarDef.Create("virology.contamination_food_puddle_per_unit", 0.03f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationFoodPuddleMaximum =
        CVarDef.Create("virology.contamination_food_puddle_maximum", 1.2f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Standing water is the only floor source fungus gets, so it is deliberately weak -
    /// well under a rotting corpse's fungal share.
    /// </summary>
    public static readonly CVarDef<float> VirologyContaminationWaterPuddlePerUnit =
        CVarDef.Create("virology.contamination_water_puddle_per_unit", 0.01f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationWaterPuddleMaximum =
        CVarDef.Create("virology.contamination_water_puddle_maximum", 0.4f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Water below this volume is ignored entirely. Mopping leaves small smears of water
    /// behind, and cleaning up blood must not breed fungus as a side effect.
    /// </summary>
    public static readonly CVarDef<float> VirologyContaminationWaterPuddleMinimumVolume =
        CVarDef.Create("virology.contamination_water_puddle_minimum_volume", 25f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationMoldPuddlePerUnit =
        CVarDef.Create("virology.contamination_mold_puddle_per_unit", 0.06f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationMoldPuddleMaximum =
        CVarDef.Create("virology.contamination_mold_puddle_maximum", 1.8f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationOrganicTrash =
        CVarDef.Create("virology.contamination_organic_trash", 0.1f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationDeadPlant =
        CVarDef.Create("virology.contamination_dead_plant", 0.75f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationViralCarrier =
        CVarDef.Create("virology.contamination_viral_carrier", 0.5f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationInfectionRadius =
        CVarDef.Create("virology.contamination_infection_radius", 1.5f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationInfectionThreshold =
        CVarDef.Create("virology.contamination_infection_threshold", 2.4f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationInfectionChanceScale =
        CVarDef.Create("virology.contamination_infection_chance_scale", 1f / 60f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationPuddleInfectionChance =
        CVarDef.Create("virology.contamination_puddle_infection_chance", 0.01f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Chance for a symptomatic fungal host to shed a patch each time its shedding
    /// interval comes around. Luck decides, so there is no fixed patch budget.
    /// </summary>
    public static readonly CVarDef<float> VirologySporePatchChance =
        CVarDef.Create("virology.spore_patch_chance", 0.5f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// How often a fungal host gets one roll to shed a patch. Combined with the patch
    /// lifetime this decides how long a trail a carrier leaves behind them.
    /// </summary>
    public static readonly CVarDef<float> VirologySporePatchInterval =
        CVarDef.Create("virology.spore_patch_interval", 120f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologySporePatchLifetime =
        CVarDef.Create("virology.spore_patch_lifetime", 600f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologySporePatchContamination =
        CVarDef.Create("virology.spore_patch_contamination", 0.9f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologySporePatchInfectionChance =
        CVarDef.Create("virology.spore_patch_infection_chance", 0.04f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyEmergentAntagChance =
        CVarDef.Create("virology.emergent_antag_chance", 0.4f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyEmergentAntagPrevalenceCap =
        CVarDef.Create("virology.emergent_antag_prevalence_cap", 0.08f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyEmergentAntagTransmissibilityMultiplier =
        CVarDef.Create("virology.emergent_antag_transmissibility_multiplier", 0.7f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyEmergentAntagProtectionBypassMultiplier =
        CVarDef.Create("virology.emergent_antag_protection_bypass_multiplier", 0.5f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyEmergentAntagStageDelayMultiplier =
        CVarDef.Create("virology.emergent_antag_stage_delay_multiplier", 1.25f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> VirologyEmergentAntagMinExtraSymptoms =
        CVarDef.Create("virology.emergent_antag_min_extra_symptoms", 1, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> VirologyEmergentAntagMaxExtraSymptoms =
        CVarDef.Create("virology.emergent_antag_max_extra_symptoms", 2, CVar.SERVERONLY | CVar.ARCHIVE);
}
