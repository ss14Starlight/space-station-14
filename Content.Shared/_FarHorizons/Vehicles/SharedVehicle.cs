using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Vehicles;

[Serializable, NetSerializable]
public enum VehicleVisualLayers : byte
{
    Base
}

[Serializable, NetSerializable]
public enum VehicleVisuals : byte
{
    VisualState
}

[Serializable, NetSerializable]
public enum VehicleVisualState : byte
{
    Normal,
    Moving,
    Broken
}