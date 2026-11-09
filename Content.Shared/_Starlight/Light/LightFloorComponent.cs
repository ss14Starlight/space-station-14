using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Light;

/// <summary>
/// Marks a powered light whose bulb can only be reached while its wires panel is open.
/// </summary>
[RegisterComponent]
public sealed partial class LightFloorComponent : Component
{
    /// <summary>
    /// Scales the brightness of the fitted bulb. Light floors are laid side by side, so each one has to be dim.
    /// </summary>
    [DataField]
    public float EnergyMultiplier = 0.6f;

    /// <summary>
    /// Caps how far the fitted bulb's light reaches, in tiles, so a whole floor of them does not stack up.
    /// </summary>
    [DataField]
    public float MaxRadius = 4f;
}

/// <summary>
/// Raised on a powered light before it is lit, so the fixture can change the brightness and reach its bulb gives.
/// </summary>
[ByRefEvent]
public record struct GetLightFixtureLevelEvent(float LightEnergy, float LightRadius);

/// <summary>
/// Sprite layers of the closed cover, drawn over the regular powered light layers.
/// </summary>
[Serializable, NetSerializable]
public enum LightFloorLayers : byte
{
    Cover,
    CoverGlow
}
