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
    public static readonly CVarDef<bool> AdminGhostJobIcons =
        CVarDef.Create("ui.admin_ghost_job_icons", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to see mindshield icons as admin ghost.
    /// </summary>
    public static readonly CVarDef<bool> AdminGhostMindshieldIcons =
        CVarDef.Create("ui.admin_ghost_mindshield_icons", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to see criminal record icons as admin ghost.
    /// </summary>
    public static readonly CVarDef<bool> AdminGhostCriminalRecordIcons =
        CVarDef.Create("ui.admin_ghost_criminal_record_icons", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to see faction icons as admin ghost.
    /// </summary>
    public static readonly CVarDef<bool> AdminGhostFactionIcons =
        CVarDef.Create("ui.admin_ghost_faction_icons", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to see health bars as admin ghost.
    /// </summary>
    public static readonly CVarDef<bool> AdminGhostHealthBars =
        CVarDef.Create("ui.admin_ghost_health_bars", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to see health icons as admin ghost.
    /// </summary>
    public static readonly CVarDef<bool> AdminGhostHealthIcons =
        CVarDef.Create("ui.admin_ghost_health_icons", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to see satiation icons as admin ghost.
    /// </summary>
    public static readonly CVarDef<bool> AdminGhostSatiationIcons =
        CVarDef.Create("ui.admin_ghost_satiation_icons", true, CVar.CLIENTONLY | CVar.ARCHIVE);

}
