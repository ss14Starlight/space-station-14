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

    [DataField]
    public Color? Accent;

    [DataField]
    public float BorderThickness = 1f;

    [DataField]
    public StyleThemeBox? Window;

    [DataField]
    public StyleThemeBox? Header;

    [DataField]
    public StyleThemeBox? Button;

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

    [DataField]
    public Color Positive;

    [DataField]
    public Color Negative;

    /// <summary>
    /// How strongly the accent tints pressed and selected elements, from 0 (no tint) to 1 (pure accent).
    /// </summary>
    [DataField]
    public float AccentTint = 0.35f;
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
