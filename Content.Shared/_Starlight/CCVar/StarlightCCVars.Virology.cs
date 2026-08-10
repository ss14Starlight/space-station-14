using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<bool> VirologyRespawnOnExtinction =
        CVarDef.Create("virology.respawn_on_extinction", false, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationSampleInterval =
        CVarDef.Create("virology.contamination_sample_interval", 10f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationRottingCorpse =
        CVarDef.Create("virology.contamination_rotting_corpse", 3f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationRottenFood =
        CVarDef.Create("virology.contamination_rotten_food", 1.5f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationBiologicalPuddlePerUnit =
        CVarDef.Create("virology.contamination_biological_puddle_per_unit", 0.06f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationBiologicalPuddleMaximum =
        CVarDef.Create("virology.contamination_biological_puddle_maximum", 2.4f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationFoodPuddlePerUnit =
        CVarDef.Create("virology.contamination_food_puddle_per_unit", 0.03f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationFoodPuddleMaximum =
        CVarDef.Create("virology.contamination_food_puddle_maximum", 1.2f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationWaterPuddlePerUnit =
        CVarDef.Create("virology.contamination_water_puddle_per_unit", 0.01f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationWaterPuddleMaximum =
        CVarDef.Create("virology.contamination_water_puddle_maximum", 0.4f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VirologyContaminationWaterPuddleMinimumVolume =
        CVarDef.Create("virology.contamination_water_puddle_minimum_volume", 25f, CVar.SERVERONLY | CVar.ARCHIVE); // Water below this volume is ignored entirely.

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
}
