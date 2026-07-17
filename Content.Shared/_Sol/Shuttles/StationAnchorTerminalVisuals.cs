using Content.Shared._Sol.Shuttles.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Shuttles;

[Serializable, NetSerializable]
public enum StationAnchorTerminalVisuals : byte
{
    Broadcasting,
    Speaker
}

[Serializable, NetSerializable]
public enum StationAnchorTerminalVisualLayers : byte
{
    Broadcasting,
    Speaker
}
