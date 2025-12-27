using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Vehicles;

[Serializable, NetSerializable]
public enum VehicleVisualLayers : byte
{
    AutoAnimate,
}

[Serializable, NetSerializable]
public enum VehicleVisuals : byte
{
    /// <summary>
    /// Whether the wheels should be turning
    /// </summary>
    AutoAnimate
}