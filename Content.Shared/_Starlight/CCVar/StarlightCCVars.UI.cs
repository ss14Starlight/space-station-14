using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;
public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Whether or not to automatically add punctuation to the end of a sentence as a player character.
    /// </summary>
    public static readonly CVarDef<bool> AutoPunctuate =
    CVarDef.Create("ic.auto_punctuate", true, CVar.REPLICATED | CVar.CLIENT | CVar.ARCHIVE);

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

    /// <summary>
    /// The theme of the interface.
    /// </summary>
    public static readonly CVarDef<string> StyleTheme =
        CVarDef.Create("ui.style_theme", "NanoTrasen", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Accent color of the interface: window titles, headings, selected tabs, pressed buttons.
    /// </summary>
    public static readonly CVarDef<string> StyleAccent =
        CVarDef.Create("ui.style_accent", "#D4D4D8", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Use <see cref="StyleAccent"/> instead of the accent of the selected theme.
    /// </summary>
    public static readonly CVarDef<bool> StyleCustomAccent =
        CVarDef.Create("ui.style_custom_accent", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Show the old drag and drop job priority columns in the lobby, under the round info.
    /// </summary>
    public static readonly CVarDef<bool> LobbyJobColumns =
        CVarDef.Create("ui.lobby_job_columns", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> LobbyBackground =
        CVarDef.Create("ui.lobby_background", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Width of the chat column in the lobby, dragged by its left edge.
    /// </summary>
    public static readonly CVarDef<int> LobbyChatWidth =
        CVarDef.Create("ui.lobby_chat_width", 630, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Opacity of the lobby panels over the lobby art, 1 is fully opaque.
    /// </summary>
    public static readonly CVarDef<float> LobbyOpacity =
        CVarDef.Create("ui.lobby_opacity", 0.95f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the chat in the lobby is collapsed to its header.
    /// </summary>
    public static readonly CVarDef<bool> LobbyChatCollapsed =
        CVarDef.Create("ui.lobby_chat_collapsed", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the ready and observe block sits above the lobby chat instead of below it.
    /// </summary>
    public static readonly CVarDef<bool> LobbyReadyOnTop =
        CVarDef.Create("ui.lobby_ready_on_top", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Where each button of the top menu bar goes: <c>Name=Pinned|Auto|Menu|Hidden;...</c>, see BarLayout.
    /// Buttons left out are Auto: on the bar while it fits, in the "more" menu otherwise.
    /// </summary>
    public static readonly CVarDef<string> MenuBarLayout =
        CVarDef.Create("ui.menu_bar_layout",
            "AchievementButton=Menu;ReportBugButton=Menu;SandboxButton=Menu",
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// When the top menu bar shows keybinds on its buttons: Always, Hold (while Alt is held) or Never.
    /// </summary>
    public static readonly CVarDef<string> MenuBarKeyHints =
        CVarDef.Create("ui.menu_bar_key_hints", "Always", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Where each action and setting of the admin help header goes, same format as <see cref="MenuBarLayout"/>.
    /// </summary>
    public static readonly CVarDef<string> AHelpBarLayout =
        CVarDef.Create("ui.ahelp_bar_layout", "Ban=Menu;Bans=Menu;Kick=Menu;RememberSelected=Menu;Respawn=Menu",
            CVar.CLIENTONLY | CVar.ARCHIVE);

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

    /// <summary>
    /// Using a gun in hand wields it first and racks the bolt on the second press, instead of the other way around.
    /// </summary>
    public static readonly CVarDef<bool> WieldBeforeRack =
        CVarDef.Create("ui.wield_before_rack", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> RangedSightRotation =
        CVarDef.Create("ui.ranged_sight_rotation", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> HeldItemShowMode =
        CVarDef.Create("ui.held_item_show_mode", (int) Content.Shared._Starlight.CCVar.HeldItemShowMode.Always, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> SightShowBoltIndicator =
        CVarDef.Create("ui.sight_show_bolt_indicator", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether a gauge of the held gun's heat is drawn next to the sight.
    /// </summary>
    public static readonly CVarDef<bool> SightShowHeatIndicator =
        CVarDef.Create("ui.sight_show_heat_indicator", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> SightShowJamIndicator =
        CVarDef.Create("ui.sight_show_jam_indicator", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> MeleeSightRotation =
        CVarDef.Create("ui.melee_sight_rotation", true, CVar.CLIENTONLY | CVar.ARCHIVE);

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

public enum HeldItemShowMode
{
    Always = 0,
    CombatModeOnly = 1,
    OutsideCombatModeOnly = 2,
    Never = 3,
}
