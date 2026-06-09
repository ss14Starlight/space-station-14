using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;
public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Minimum width of the separated chat window.
    /// </summary>
    public static readonly CVarDef<int> ChatSeparatedMinWidth =
        CVarDef.Create("ui.seperated_chat_min_width", 300, CVar.CLIENT | CVar.ARCHIVE);

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
