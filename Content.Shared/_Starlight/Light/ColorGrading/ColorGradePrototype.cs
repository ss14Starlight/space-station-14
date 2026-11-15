using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Light.ColorGrading;

/// <summary>
/// A colour grade for the whole view, used by zones and alert levels.
/// When the player stands in a zone during an alert, the zone's grade is applied first and the alert's on top.
/// </summary>
[Prototype]
public sealed partial class ColorGradePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Multiplies every colour. Slightly off white, like "#fff4e8" for warm light.
    /// </summary>
    [DataField]
    public Color Tint = Color.White;

    /// <summary>
    /// Added to dark colours and fading out towards white, so shadows take this colour while highlights stay clean.
    /// Keep it dark, "#0a0000" already reads as red shadows.
    /// </summary>
    [DataField]
    public Color Shadows = Color.Black;

    /// <summary>
    /// 1 leaves colours as they are, below 1 washes them out, above 1 makes them richer.
    /// </summary>
    [DataField]
    public float Saturation = 1f;

    /// <summary>
    /// 1 leaves the picture as it is, above 1 pushes darks and lights apart around middle grey.
    /// </summary>
    [DataField]
    public float Contrast = 1f;
}
