using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.StationRadio.UI;

public sealed class PixelProgressBar : Control
{
    private static readonly Color Border = Color.FromHex("#141011");
    private static readonly Color Track = Color.FromHex("#2c2e36");
    private static readonly Color Fill = Color.FromHex("#4fc257");
    private static readonly Color FillPaused = Color.FromHex("#e3c447");

    public float Progress;

    public bool Paused;

    public PixelProgressBar()
    {
        MinSize = new Vector2(0, 6);
        HorizontalExpand = true;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var px = MathF.Max(1f, MathF.Round(UIScale));
        if (PixelWidth < 3 * px || PixelHeight < 3 * px)
            return;

        handle.DrawRect(new UIBox2(0, 0, PixelWidth, PixelHeight), Border);
        var inner = new UIBox2(px, px, PixelWidth - px, PixelHeight - px);
        handle.DrawRect(inner, Track);

        var width = MathF.Round(inner.Width * Math.Clamp(Progress, 0f, 1f));
        if (width > 0)
            handle.DrawRect(new UIBox2(inner.Left, inner.Top, inner.Left + width, inner.Bottom), Paused ? FillPaused : Fill);
    }
}
