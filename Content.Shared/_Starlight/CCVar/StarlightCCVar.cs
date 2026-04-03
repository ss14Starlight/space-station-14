using Robust.Shared.Configuration;
using Content.Shared.CCVar.CVarAccess;
using Content.Shared.Administration;

namespace Content.Shared.Starlight.CCVar;

[CVarDefs]
public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Basic CCVars
    /// </summary>
    public static readonly CVarDef<string> LobbyChangelogsList =
        CVarDef.Create("lobby_changelog.list", "ChangelogStarlight.yml,Changelog.yml", CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> ServerName =
        CVarDef.Create("lobby.server_name", "☆ Starlight ☆", CVar.SERVER | CVar.REPLICATED);

    [CVarControl(AdminFlags.Adminchat)]
    public static readonly CVarDef<string> OverrideGamemodeName =
        CVarDef.Create("lobby.gamemode_name_override", "", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
    [CVarControl(AdminFlags.Adminchat)]
    public static readonly CVarDef<string> OverrideGamemodeDescription =
        CVarDef.Create("lobby.gamemode_desc_override", "", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Whether the no EORG popup is enabled.
    /// </summary>
    public static readonly CVarDef<bool> RoundEndNoEorgPopup =
        CVarDef.Create("game.round_end_eorg_popup_enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Skip the no EORG popup.
    /// </summary>
    public static readonly CVarDef<bool> SkipRoundEndNoEorgPopup =
        CVarDef.Create("game.skip_round_end_eorg_popup", false, CVar.CLIENTONLY | CVar.ARCHIVE);

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
}
