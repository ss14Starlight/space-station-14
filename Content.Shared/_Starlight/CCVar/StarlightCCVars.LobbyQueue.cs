using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Daiquiri
    /// </summary>
    public static readonly CVarDef<string> LobbyQueueRemoved =
        CVarDef.Create("lobby.queue_removed", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Daiquiri
    /// </summary>
    public static readonly CVarDef<string> LobbyQueueTouched =
        CVarDef.Create("lobby.queue_touched", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Daiquiri
    /// </summary>
    public static readonly CVarDef<string> LobbyQueueOrder =
        CVarDef.Create("lobby.queue_order", "", CVar.CLIENTONLY | CVar.ARCHIVE);
    /// <summary>
    /// Daiquiri
    /// </summary>
    public static readonly CVarDef<string> LobbyQueueAdded =
        CVarDef.Create("lobby.queue_added", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Last lobby track that was playing, so reconnecting players resume the same song.
    /// </summary>
    public static readonly CVarDef<string> LobbyLastTrack =
        CVarDef.Create("lobby.last_track", "", CVar.CLIENTONLY | CVar.ARCHIVE);
}
