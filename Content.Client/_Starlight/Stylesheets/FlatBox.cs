using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client._Starlight.Stylesheets;

public static class FlatBox
{
    public static StyleBoxFlat Fill(Color color)
        => new() { BackgroundColor = color };

    public static StyleBoxFlat Bordered(Color color, Color border, float thickness = 1)
        => new()
        {
            BackgroundColor = color,
            BorderColor = border,
            BorderThickness = new Thickness(thickness),
        };

    public static StyleBox Chamfered(Color color, Color border, float thickness)
        => new ChamferedBox(color, border, thickness);

    public static StyleBox BorderedOrChamfered(bool classic, Color color, Color border, float thickness)
        => classic ? Chamfered(color, border, thickness) : Bordered(color, border, thickness);

    public static StyleBoxFlat Underlined(Color color, Color border, float thickness)
        => new()
        {
            BackgroundColor = color,
            BorderColor = border,
            BorderThickness = new Thickness(0, 0, 0, thickness),
        };

    public static T WithContentMargin<T>(this T box, StyleBox.Margin margin, float value) where T : StyleBox
    {
        box.SetContentMarginOverride(margin, value);
        return box;
    }

    public static T WithPadding<T>(this T box, StyleBox.Margin margin, float value) where T : StyleBox
    {
        box.SetPadding(margin, value);
        return box;
    }

    public sealed class ChamferedBox : StyleBox
    {
        public Color BackgroundColor { get; set; }
        public Color BorderColor { get; set; }
        public float BorderThickness { get; set; }

        public ChamferedBox(Color background, Color border, float thickness)
        {
            BackgroundColor = background;
            BorderColor = border;
            BorderThickness = thickness;
        }

        public ChamferedBox(ChamferedBox other) : base(other)
        {
            BackgroundColor = other.BackgroundColor;
            BorderColor = other.BorderColor;
            BorderThickness = other.BorderThickness;
        }

        protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
        {
            if (box.Width <= 0f || box.Height <= 0f)
                return;

            var cut = MathF.Min(uiScale, MathF.Min(box.Width, box.Height) / 2f);
            var thickness = MathF.Max(BorderThickness * uiScale, 0f);

            if (thickness <= 0f)
            {
                DrawChamfered(handle, box, cut, BackgroundColor);
                return;
            }

            if (thickness * 2f >= MathF.Min(box.Width, box.Height))
            {
                DrawChamfered(handle, box, cut, BorderColor);
                return;
            }

            var px = uiScale;
            if (BorderThickness >= 2f)
            {
                var border = BorderColor;
                var outer = new Color(border.R * 0.85f, border.G * 0.85f, border.B * 0.85f, border.A);

                Band(handle, box.Left + px, box.Top, box.Right - px, box.Top + px, outer);
                Band(handle, box.Left + px, box.Bottom - px, box.Right - px, box.Bottom, outer);
                Band(handle, box.Left, box.Top + cut, box.Left + px, box.Bottom - cut, outer);
                Band(handle, box.Right - px, box.Top + cut, box.Right, box.Bottom - cut, outer);

                var inner = Deflate(box, px);
                var band = thickness - px;
                Band(handle, inner.Left, inner.Top, inner.Left + band, inner.Bottom, BorderColor);
                Band(handle, inner.Right - band, inner.Top, inner.Right, inner.Bottom, BorderColor);
                Band(handle, inner.Left + band, inner.Top, inner.Right - band, inner.Top + band, BorderColor);
                Band(handle, inner.Left + band, inner.Bottom - band, inner.Right - band, inner.Bottom, BorderColor);
            }
            else
            {
                Band(handle, box.Left + thickness, box.Top, box.Right - thickness, box.Top + thickness, BorderColor);
                Band(handle, box.Left + thickness, box.Bottom - thickness, box.Right - thickness, box.Bottom, BorderColor);
                Band(handle, box.Left, box.Top + cut, box.Left + thickness, box.Bottom - cut, BorderColor);
                Band(handle, box.Right - thickness, box.Top + cut, box.Right, box.Bottom - cut, BorderColor);
            }

        var fill = Deflate(box, thickness);
        Band(handle, fill.Left, fill.Top, fill.Right, fill.Bottom, BackgroundColor);
    }

        private static void Band(DrawingHandleScreen handle, float left, float top, float right, float bottom, Color color)
        {
            if (color.A <= 0f || right <= left || bottom <= top)
                return;

            handle.DrawRect(new UIBox2(left, top, right, bottom), color);
        }

    private static void DrawChamfered(DrawingHandleScreen handle, UIBox2 box, float cut, Color color)
    {
        Band(handle, box.Left + cut, box.Top, box.Right - cut, box.Bottom, color);
        Band(handle, box.Left, box.Top + cut, box.Left + cut, box.Bottom - cut, color);
        Band(handle, box.Right - cut, box.Top + cut, box.Right, box.Bottom - cut, color);
    }

    private static UIBox2 Deflate(UIBox2 box, float amount) => new(
        MathF.Min(box.Left + amount, box.Right),
        MathF.Min(box.Top + amount, box.Bottom),
        MathF.Max(box.Right - amount, box.Left),
        MathF.Max(box.Bottom - amount, box.Top));

    protected override float GetDefaultContentMargin(Margin margin) => BorderThickness;
    }
}
