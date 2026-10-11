using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Light;

/// <summary>
/// Marks a powered light whose bulb can only be reached while its wires panel is open.
/// </summary>
[RegisterComponent]
public sealed partial class LightFloorComponent : Component;

/// <summary>
/// Sprite layers of the closed cover, drawn over the regular powered light layers.
/// </summary>
[Serializable, NetSerializable]
public enum LightFloorLayers : byte
{
    Cover,
    CoverGlow
}
