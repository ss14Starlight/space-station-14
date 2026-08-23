using System.Numerics;
using Content.Client._Starlight.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Railroading;

internal static class CardWindow
{
    private static readonly Vector2 _emptySize = new(320, 96);
    private static readonly Vector2 _restrictedSize = new(360, 128);

    /// <summary>
    /// Fills a window with the placeholder shown when there is nothing to pick.
    /// </summary>
    internal static void RenderEmpty(SLWindow window, bool restricted)
    {
        var size = restricted ? _restrictedSize : _emptySize;

        window.Resizable = false;
        window.Contents.SetSize = size;
        window.Contents.MinSize = size;
        window.Contents.MaxSize = size;

        window.Title = Loc.GetString("card-selection-window-title");

        window.Contents.RemoveAllChildren();
        window.Box(BoxContainer.LayoutOrientation.Vertical, box =>
        {
            box.Align = BoxContainer.AlignMode.Center;
            box.HorizontalExpand = true;
            box.VerticalExpand = true;
            box.RichText(label =>
            {
                label.WithText(Loc.GetString(restricted ? "card-selection-restricted" : "card-selection-no-cards"));
                label.HorizontalAlignment = Control.HAlignment.Center;
                label.VerticalAlignment = Control.VAlignment.Center;
                label.MaxWidth = size.X - 24;
            });
        });
    }
}

/// <summary>
/// Counts down to the moment the offered hand is discarded.
/// </summary>
internal sealed partial class CardCountdown : Label
{
    private static readonly Color _urgentColor = Color.FromHex("#FF7575");
    private const double UrgentThreshold = 10;

    [Dependency] private IGameTiming _timing = default!;

    public TimeSpan? Deadline;

    public CardCountdown()
    {
        IoCManager.InjectDependencies(this);
        HorizontalAlignment = HAlignment.Center;
        Margin = new Thickness(0, 4);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Deadline is not { } deadline)
        {
            Visible = false;
            return;
        }

        Visible = true;

        var remaining = deadline - _timing.CurTime;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var seconds = Math.Ceiling(remaining.TotalSeconds);
        Text = Loc.GetString("card-selection-timer", ("seconds", seconds));
        Modulate = seconds <= UrgentThreshold ? _urgentColor : Color.White;
    }
}
