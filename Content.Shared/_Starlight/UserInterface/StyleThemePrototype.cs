using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.UserInterface;

/// <summary>
/// A prototype that defines a style theme for the user interface.
/// </summary>
[Prototype]
public sealed partial class StyleThemePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Sort order in the options menu.
    /// </summary>
    [DataField]
    public int Order;

    /// <summary>
    /// Whether this theme has dark text on light surfaces.
    /// </summary>
    [DataField]
    public bool Light;

    [DataField]
    public bool Stock;

    /// <summary>
    /// Primary accent color, controlling header text.
    /// If secondaryAccent is not specified, falls back to Accent.
    /// </summary>
    [DataField]
    public Color? Accent;

    /// <summary>
    /// Border width for tooltips/context menus.
    /// </summary>
    [DataField]
    public float BorderThickness = 1f;

    /// <summary>
    /// Border width for text inputs and inset panels.
    /// </summary>
    [DataField]
    public float SecondaryBorderThickness = 1f;

    [DataField]
    public StyleThemeBox? Window;

    [DataField]
    public StyleThemeBox? Header;

    [DataField]
    public StyleThemeBox? Button;

    /// <summary>
    /// When true, the theme keeps the classic textured window, panel, and button borders.
    /// </summary>
    [DataField]
    public bool ClassicDecor;

    /// <summary>
    /// Theme for the in-game HUD screen, if it should differ from this one. HUD textures are dark, so light themes
    /// keep a dark HUD and only restyle windows and menus.
    /// </summary>
    [DataField]
    public ProtoId<StyleThemePrototype>? Hud;

    [DataField]
    public Color Background;

    [DataField]
    public Color Surface;

    /// <summary>
    /// Inspection menu background.
    /// If not specified, falls back to surface.
    /// </summary>
    [DataField]
    public Color? SurfaceInspect;

    [DataField]
    public Color SurfaceRaised;

    [DataField]
    public Color Element;

    [DataField]
    public Color ElementHovered;

    [DataField]
    public Color ElementDisabled;

    [DataField]
    public Color Border;

    [DataField]
    public Color Text;

    [DataField]
    public Color TextMuted;

    [DataField]
    public Color TextDisabled;

    /// <summary>
    /// Placeholder text color for text inputs.
    /// If textPlaceholder is not specified, falls back to TextDisabled.
    /// </summary>
    [DataField]
    public Color? TextPlaceholder;

    [DataField]
    public Color Positive;

    [DataField]
    public Color Negative;

    /// <summary>
    /// How strongly the accent tints pressed and selected elements, from 0 (no tint) to 1 (pure accent).
    /// </summary>
    [DataField]
    public float AccentTint = 0.35f;

    /// <summary>
    /// Background for FancyWindow bodies. When null, window bodies
    /// keep the shared BackgroundPanel styling.
    /// </summary>
    [DataField]
    public Color? WindowBackground;

    /// <summary>
    /// When true, display most colors directly without any further filtering.
    /// </summary>
    [DataField]
    public bool RawColors;

    /// <summary>
    /// Secondary accent color, for pressed and selected buttons.
    /// If secondaryAccent is not specified, falls back to Accent.
    /// </summary>
    [DataField]
    public Color? SecondaryAccent;

    /// <summary>
    /// Tooltip text color. If textTooltip is not specified, falls back to text.
    /// </summary>
    [DataField]
    public Color? TextTooltip;
}

[DataDefinition]
public sealed partial class StyleThemeBox
{
    [DataField(required: true)]
    public ResPath Texture;

    [DataField]
    public float[] Patch = [0f];

    [DataField]
    public bool Tile;

    [DataField]
    public float Scale = 1f;

    [DataField]
    public float Expand;

    [DataField]
    public float? Content;

    [DataField]
    public Color Modulate = Color.White;
}
