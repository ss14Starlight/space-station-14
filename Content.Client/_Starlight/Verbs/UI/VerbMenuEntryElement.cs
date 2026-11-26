using Content.Client.ContextMenu.UI;

namespace Content.Client._Starlight.Verbs.UI;

/// <summary>
/// A verb menu entry that runs a client-side action instead of a verb.
/// </summary>
public abstract class VerbMenuEntryElement(string text) : ContextMenuElement(text)
{
    /// <summary>
    /// Invoked when clicked.
    /// </summary>
    public Action? OnUse;

    /// <summary>
    /// Whether clicking closes the menu. If not, it closes once the mouse leaves it.
    /// </summary>
    public bool CloseMenu = true;
}
