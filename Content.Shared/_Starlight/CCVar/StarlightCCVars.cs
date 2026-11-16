using Robust.Shared.Configuration;
using Content.Shared.CCVar.CVarAccess;
using Content.Shared.Administration;

namespace Content.Shared._Starlight.CCVar;

[CVarDefs]
public sealed partial class StarlightCCVars
{
    /// Basic CCVars
    public static readonly CVarDef<string> LobbyChangelogsList =
        CVarDef.Create("lobby_changelog.list", "ChangelogStarlight.yml,Changelog.yml", CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> ServerName =
        CVarDef.Create("lobby.server_name", "Дайкири", CVar.SERVER | CVar.REPLICATED);

    [CVarControl(AdminFlags.Adminchat)]
    public static readonly CVarDef<string> OverrideGamemodeName =
        CVarDef.Create("lobby.gamemode_name_override", "", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
    [CVarControl(AdminFlags.Adminchat)]
    public static readonly CVarDef<string> OverrideGamemodeDescription =
        CVarDef.Create("lobby.gamemode_desc_override", "", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// Whether the no EORG popup is enabled.
    public static readonly CVarDef<bool> RoundEndNoEorgPopup =
        CVarDef.Create("game.round_end_eorg_popup_enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// Skip the no EORG popup.
    public static readonly CVarDef<bool> SkipRoundEndNoEorgPopup =
        CVarDef.Create("game.skip_round_end_eorg_popup", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> UIGlassTheme =
        CVarDef.Create("ui.glass_theme", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> UIGlassTransparencyEnabled =
        CVarDef.Create("ui.glass_transparency_enabled", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> UIGlassTransparency =
        CVarDef.Create("ui.glass_transparency", 20, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> UIGlassAccentEnabled =
        CVarDef.Create("ui.glass_accent_enabled", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> UIGlassAccent =
        CVarDef.Create("ui.glass_accent_v2", "#545562", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// Whether the first-run liquid glass prompt was already shown.
    public static readonly CVarDef<bool> UIGlassThemeSeen =
        CVarDef.Create("ui.glass_theme_seen", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> UILiquidGlassEnabled =
        CVarDef.Create("ui.liquid_glass_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> UILiquidGlassBlur =
        CVarDef.Create("ui.liquid_glass_blur", 50, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// </summary>
    public static readonly CVarDef<int> UILiquidGlassRefraction =
        CVarDef.Create("ui.liquid_glass_refraction", 43, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// </summary>
    public static readonly CVarDef<int> UILiquidGlassHighlight =
        CVarDef.Create("ui.liquid_glass_highlight", 43, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// </summary>
    public static readonly CVarDef<int> UILiquidGlassOpacity =
        CVarDef.Create("ui.liquid_glass_opacity", 95, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// </summary>
    public static readonly CVarDef<int> UILiquidGlassTint =
        CVarDef.Create("ui.liquid_glass_tint", 30, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// </summary>
    public static readonly CVarDef<bool> UILiquidGlassTintEnabled =
        CVarDef.Create("ui.liquid_glass_tint_enabled", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// </summary>
    public static readonly CVarDef<string> UILiquidGlassTintColor =
        CVarDef.Create("ui.liquid_glass_tint_color", "#7A8AA8", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// </summary>
    public static readonly CVarDef<int> UILiquidGlassCornerRadius =
        CVarDef.Create("ui.liquid_glass_corner_radius", 55, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// </summary>
    public static readonly CVarDef<int> UILiquidGlassQuality =
        CVarDef.Create("ui.liquid_glass_quality", 2, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// </summary>
    public static readonly CVarDef<bool> UILiquidGlassAnimate =
        CVarDef.Create("ui.liquid_glass_animate", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How long to display the EORG popup for.
    /// </summary>
    public static readonly CVarDef<float> RoundEndNoEorgPopupTime =
        CVarDef.Create("game.round_end_eorg_popup_time", 5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> ConfigFile =
        CVarDef.Create("config.file", "config.yml", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Disables pathfinding system updates and path generation. Intended for integration tests.
    /// </summary>
    public static readonly CVarDef<bool> DisablePathfinding =
        CVarDef.Create("starlight.pathfinding.disable", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> DisableLoadMapRule =
        CVarDef.Create("starlight.loadmaprule.disable", false, CVar.SERVERONLY);

    /// <summary>
    /// Forces the Tester tab to appear in the ghost themes menu even without the Tester role
    /// </summary>
    public static readonly CVarDef<bool> ForceTestersTab =
        CVarDef.Create("ghosttheme.forcetesterstab", false, CVar.CLIENTONLY | CVar.ARCHIVE,
            "forces the Testers ghost theme tab to be visible even if you have no unlocks.");

    [CVarControl(AdminFlags.Adminchat)]
    public static readonly CVarDef<float> StaggeredSayCooldown =
        CVarDef.Create("commands.staggered_say_cooldown", 1.5f, CVar.SERVERONLY | CVar.ARCHIVE,
            "The number of seconds a user must wait before being able to execute ssay again.");
}
