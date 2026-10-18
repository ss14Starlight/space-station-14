using Robust.Client.Graphics;

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
}
