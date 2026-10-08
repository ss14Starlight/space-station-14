using Robust.Shared.Prototypes;

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

    /// <summary>
    /// Theme for the in-game HUD screen, if it should differ from this one. HUD textures are dark, so light themes
    /// keep a dark HUD and only restyle windows and menus.
    /// </summary>
    [DataField]
    public ProtoId<StyleThemePrototype>? Hud;

    [DataField(required: true)]
    public Color Background;

    [DataField(required: true)]
    public Color Surface;

    [DataField(required: true)]
    public Color SurfaceRaised;

    [DataField(required: true)]
    public Color Element;

    [DataField(required: true)]
    public Color ElementHovered;

    [DataField(required: true)]
    public Color ElementDisabled;

    [DataField(required: true)]
    public Color Border;

    [DataField(required: true)]
    public Color Text;

    [DataField(required: true)]
    public Color TextMuted;

    [DataField(required: true)]
    public Color TextDisabled;

    [DataField(required: true)]
    public Color Positive;

    [DataField(required: true)]
    public Color Negative;

    /// <summary>
    /// How strongly the accent tints pressed and selected elements, from 0 (no tint) to 1 (pure accent).
    /// </summary>
    [DataField]
    public float AccentTint = 0.35f;
}
