using Robust.Shared.Configuration;

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
    
    /// <summary>
    /// Increases character slots limit to 30
    /// </summary>
    public static readonly CVarDef<int>
            GameMaxCharacterSlots = CVarDef.Create("game.maxcharacterslots", 30, CVar.ARCHIVE | CVar.SERVERONLY);    
    /// <summary>
    /// Making everyone a pacifist at the end of a round.
    /// </summary>
    public static readonly CVarDef<bool> PeacefulRoundEnd =
        CVarDef.Create("game.peaceful_end", true, CVar.SERVERONLY);
}
