using System.Numerics;
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

            var cut = MathF.Min(1f * uiScale, MathF.Min(box.Width, box.Height) / 2f);
            var thickness = MathF.Max(BorderThickness * uiScale, 0f);

            if (thickness <= 0f)
            {
                DrawChamfered(handle, box, cut, BackgroundColor);
                return;
            }

            var border = BorderColor;
            var outer = new Color(border.R * 0.85f, border.G * 0.85f, border.B * 0.85f, border.A);

            if (BorderThickness >= 2f)
            {
                DrawChamfered(handle, box, cut, outer);
                DrawChamfered(handle, Deflate(box, 1f * uiScale), cut, BorderColor);
            }
            else
            {
                DrawChamfered(handle, box, cut, BorderColor);
            }

            DrawRect(handle, Deflate(box, thickness), BackgroundColor);
        }

        private static void DrawChamfered(DrawingHandleScreen handle, UIBox2 box, float cut, Color color)
        {
            if (color.A <= 0f || box.Width <= 0f || box.Height <= 0f)
                return;

            if (cut <= 0f)
            {
                handle.DrawRect(box, color);
                return;
            }

            handle.DrawRect(new UIBox2(box.Left + cut, box.Top, box.Right - cut, box.Bottom), color);
            handle.DrawRect(new UIBox2(box.Left, box.Top + cut, box.Left + cut, box.Bottom - cut), color);
            handle.DrawRect(new UIBox2(box.Right - cut, box.Top + cut, box.Right, box.Bottom - cut), color);
        }

        private static void DrawRect(DrawingHandleScreen handle, UIBox2 box, Color color)
        {
            if (color.A <= 0f || box.Width <= 0f || box.Height <= 0f)
                return;

            handle.DrawRect(box, color);
        }

        private static UIBox2 Deflate(UIBox2 box, float amount)
        {
            return new UIBox2(
                MathF.Min(box.Left + amount, box.Right),
                MathF.Min(box.Top + amount, box.Bottom),
                MathF.Max(box.Right - amount, box.Left),
                MathF.Max(box.Bottom - amount, box.Top));
        }

        protected override float GetDefaultContentMargin(Margin margin)
        {
            return BorderThickness;
        }
    }
}
