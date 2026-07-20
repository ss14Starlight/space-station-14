// Sol
using Robust.Shared.Configuration;
using Robust.Shared.Maths;

namespace Content.Shared._Sol.CCVar;

[CVarDefs]
public sealed partial class SolCCVars
{
    /// <summary>
    /// Whether the separated-chat side panel (emotes / info / languages) starts expanded.
    /// </summary>
    public static readonly CVarDef<bool> SeparatedChatSideExpanded =
        CVarDef.Create("ui.sol_separated_chat_side_expanded", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Remembered expanded height of the separated-chat side panel in virtual pixels.
    /// </summary>
    public static readonly CVarDef<float> SeparatedChatSideExpandedHeight =
        CVarDef.Create("ui.sol_separated_chat_side_expanded_height", 220f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Play a chime when a chat message matches the client's highlight keywords.
    /// </summary>
    public static readonly CVarDef<bool> ChatHighlightSound =
        CVarDef.Create("chat.sol_highlight_sound", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    // ── PlaySol website (server-only, confidential) ──────────────────────────

    /// <summary>
    /// Base URL of the PlaySol site API (e.g. https://playsol.us). Empty disables posting.
    /// </summary>
    public static readonly CVarDef<string> PlaySolApiBase =
        CVarDef.Create("playsol.api_base", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Long-lived deploy secret used to exchange a short-lived JWT with PlaySol.
    /// </summary>
    public static readonly CVarDef<string> PlaySolDeploySecret =
        CVarDef.Create("playsol.deploy_secret", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Full path for PlaySol token exchange (confidential). Empty disables PlaySol posting.
    /// Set only in server config — never commit the real path.
    /// </summary>
    public static readonly CVarDef<string> PlaySolAuthPath =
        CVarDef.Create("playsol.auth_path", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Path for station news ingest (appended to api_base).
    /// </summary>
    public static readonly CVarDef<string> PlaySolNewsPath =
        CVarDef.Create("playsol.news_path", "/api/v1/news", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// HEX color for news embeds on the site (matches Discord lawn green by default).
    /// </summary>
    public static readonly CVarDef<string> PlaySolNewsEmbedColor =
        CVarDef.Create("playsol.news_embed_color", Color.LawnGreen.ToHex(), CVar.SERVERONLY);

    /// <summary>
    /// If true, post each article when published mid-round. If false, post the full list at round end.
    /// </summary>
    public static readonly CVarDef<bool> PlaySolNewsSendDuringRound =
        CVarDef.Create("playsol.news_send_during_round", true, CVar.SERVERONLY);
}
