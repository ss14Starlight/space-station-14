// Sol
using Robust.Shared.Configuration;

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
}
