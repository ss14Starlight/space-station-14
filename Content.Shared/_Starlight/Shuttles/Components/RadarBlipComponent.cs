using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.Components;

/// <summary>
/// Shape rendered for a <see cref="RadarBlipComponent"/> on shuttle radar.
/// </summary>
[Serializable, NetSerializable]
public enum BlipShape : byte
{
    /// <summary>Upward-pointing filled triangle (default).</summary>
    Triangle,
    /// <summary>Filled circle.</summary>
    Circle,
    /// <summary>Filled square (axis-aligned).</summary>
    Square,
}

/// <summary>
/// Makes this entity appear as a blip on shuttle radar consoles.
/// Useful for large projectiles like artillery shells that should be visible on radar.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RadarBlipComponent : Component
{
    /// <summary>
    /// The color of the blip on radar.
    /// </summary>
    [DataField]
    public Color Color = Color.OrangeRed;

    /// <summary>
    /// Size scale of the blip (1.0 = default ~7px base size).
    /// </summary>
    [DataField]
    public float Scale = 1f;

    /// <summary>
    /// If true, the blip is only shown when the entity is NOT parented to a grid
    /// (i.e. it is actually flying through open space). Prevents static items from cluttering radar.
    /// </summary>
    [DataField]
    public bool RequireInSpace = true;

    /// <summary>
    /// Which shape to render for this blip.
    /// </summary>
    [DataField]
    public BlipShape Shape = BlipShape.Triangle;
}
