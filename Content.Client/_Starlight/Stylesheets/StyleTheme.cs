using Content.Client.Stylesheets.Colorspace;
using Content.Client.Stylesheets.Palette;
using Content.Shared._Starlight.UserInterface;

namespace Content.Client._Starlight.Stylesheets;

/// <summary>
/// A theme for a stylesheet, containing a set of colors and palettes that can be used to style UI elements.
/// </summary>
public sealed class StyleTheme
{
    /// <summary>
    /// The prototype that this theme is based on.
    /// </summary>
    public readonly StyleThemePrototype Proto;

    public readonly Color Accent;

    /// <summary>
    /// An element tinted with the accent: pressed buttons, selected list items.
    /// </summary>
    public readonly Color ElementActive;

    /// <summary>
    /// Buttons, list items, dropdowns.
    /// </summary>
    public readonly ColorPalette Primary;

    /// <summary>
    /// Panels and other surfaces.
    /// </summary>
    public readonly ColorPalette Secondary;

    /// <summary>
    /// Titles, headings, highlighted panels.
    /// </summary>
    public readonly ColorPalette Highlight;

    /// <summary>
    /// Positive status: success, good, correct.
    /// </summary>
    public readonly ColorPalette Positive;

    /// <summary>
    /// Negative status: error, bad, incorrect.
    /// </summary>
    public readonly ColorPalette Negative;

    public StyleTheme(StyleThemePrototype proto, Color accent)
    {
        Proto = proto;
        Accent = Readable(accent.WithAlpha(1f), proto.Light);
        ElementActive = proto.Element.OkBlend(Accent, proto.AccentTint);

        Primary = new ColorPalette(
            Base: proto.Element,
            LightnessShift: 0f,
            ChromaShift: 0f,
            Element: proto.Element,
            HoveredElement: proto.ElementHovered,
            PressedElement: ElementActive,
            DisabledElement: proto.ElementDisabled,
            Background: proto.Surface,
            BackgroundLight: proto.SurfaceRaised,
            BackgroundDark: proto.Background,
            Text: proto.Text,
            TextDark: proto.TextMuted);

        Secondary = Primary with
        {
            Base = proto.Surface,
            Element = proto.SurfaceRaised,
            HoveredElement = proto.Element,
            TextDark = proto.Border,
        };

        Highlight = new ColorPalette(
            Base: Accent,
            LightnessShift: 0f,
            ChromaShift: 0f,
            Element: Accent,
            HoveredElement: Accent.OkBlend(proto.Text, 0.25f),
            PressedElement: Accent.OkBlend(proto.Background, 0.25f),
            DisabledElement: proto.ElementDisabled,
            Background: proto.Surface.OkBlend(Accent, 0.15f),
            BackgroundLight: proto.SurfaceRaised.OkBlend(Accent, 0.2f),
            BackgroundDark: proto.Background.OkBlend(Accent, 0.1f),
            Text: Accent,
            TextDark: Accent.OkBlend(proto.TextMuted, 0.5f));

        Positive = StatusPalette(proto, proto.Positive);
        Negative = StatusPalette(proto, proto.Negative);
    }

    private static Color Readable(Color accent, bool light)
    {
        var lab = accent.LabFromSrgb();
        lab.X = light ? Math.Min(lab.X, 0.5f) : Math.Max(lab.X, 0.6f);
        return lab.LabToSrgb();
    }

    private static ColorPalette StatusPalette(StyleThemePrototype proto, Color color)
        => new(
            Base: color,
            LightnessShift: 0f,
            ChromaShift: 0f,
            Element: proto.Element.OkBlend(color, 0.55f),
            HoveredElement: proto.ElementHovered.OkBlend(color, 0.65f),
            PressedElement: proto.Element.OkBlend(color, 0.4f),
            DisabledElement: proto.ElementDisabled,
            Background: proto.Surface.OkBlend(color, 0.2f),
            BackgroundLight: proto.SurfaceRaised.OkBlend(color, 0.25f),
            BackgroundDark: proto.Background.OkBlend(color, 0.15f),
            Text: proto.Text.OkBlend(color, 0.75f),
            TextDark: proto.TextMuted.OkBlend(color, 0.6f));
}
