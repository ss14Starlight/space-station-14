using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;
public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Minimum width of the separated chat window.
    /// </summary>
    public static readonly CVarDef<int> ChatSeparatedMinWidth =
        CVarDef.Create("ui.seperated_chat_min_width", 300, CVar.CLIENT | CVar.ARCHIVE);

    /// <summary>
    /// A newline-separated list of saved labels for the hand labeler tool
    /// </summary>
    public static readonly CVarDef<string> HandLabelerSavedLabels =
        CVarDef.Create("ui.hand_labeler_saved_labels", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> RangedSight =
        CVarDef.Create("ui.ranged_sight", "GunSight", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> RangedSightScale =
        CVarDef.Create("ui.ranged_sight_scale", 60, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> RangedSightOffset =
        CVarDef.Create("ui.ranged_sight_offset", 50, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> SightMainColor =
        CVarDef.Create("ui.sight_main_color", Color.White.WithAlpha(0.3f).ToHex(), CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> SightSecondColor =
        CVarDef.Create("ui.sight_second_color", Color.Black.WithAlpha(0.5f).ToHex(), CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> MeleeSight =
        CVarDef.Create("ui.melee_sight", "MeleeSight", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> SightRotation =
        CVarDef.Create("ui.sight_rotation", true, CVar.CLIENTONLY | CVar.ARCHIVE);
        
    /// <summary>
    /// Whether to see job icons as admin ghost.
    /// </summary>
    public static readonly CVarDef<string> AdminGhostHudJobSetting =
        CVarDef.Create("ui.admin_ghost_job_icons", "JobAndMindShield", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to see health icons, bars as admin ghost.
    /// </summary>
    public static readonly CVarDef<string> AdminGhostHudHealthSetting =
        CVarDef.Create("ui.admin_ghost_health_icons", "Bars", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to see criminal record icons as admin ghost.
    /// </summary>
    public static readonly CVarDef<bool> AdminGhostHudShowCriminalRecordIcons =
        CVarDef.Create("ui.admin_ghost_criminal_record_icons", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to see faction icons as admin ghost.
    /// </summary>
    public static readonly CVarDef<bool> AdminGhostHudShowFactionIcons =
        CVarDef.Create("ui.admin_ghost_faction_icons", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to see satiation icons as admin ghost.
    /// </summary>
    public static readonly CVarDef<bool> AdminGhostHudShowSatiationIcons =
        CVarDef.Create("ui.admin_ghost_satiation_icons", false, CVar.CLIENTONLY | CVar.ARCHIVE);

}
