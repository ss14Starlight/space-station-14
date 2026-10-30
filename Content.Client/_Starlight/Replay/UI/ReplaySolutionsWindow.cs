using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Replay.UI;

/// <summary>
/// Read-only list of an entity's solutions, refreshed by <see cref="ReplayObserverSystem"/> as the replay plays.
/// </summary>
public sealed class ReplaySolutionsWindow : DefaultWindow
{
    private readonly RichTextLabel _label = new() { VerticalAlignment = VAlignment.Top };
    private string? _markup;

    public ReplaySolutionsWindow()
    {
        MinSize = new Vector2(280, 120);
        SetSize = new Vector2(320, 260);
        Contents.AddChild(new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            Children = { _label },
        });
    }

    public void SetContents(FormattedMessage msg)
    {
        // Skip relayout when nothing changed, which is most frames.
        var markup = msg.ToMarkup();
        if (markup == _markup)
            return;

        _markup = markup;
        _label.SetMessage(msg);
    }
}
