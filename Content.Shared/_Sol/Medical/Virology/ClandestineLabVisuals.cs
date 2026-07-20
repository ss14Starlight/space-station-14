using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology;

/// <summary>
/// Appearance keys for clandestine bioterror lab machines.
/// </summary>
[Serializable, NetSerializable]
public enum ClandestineLabVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum ClandestineLabVisualState : byte
{
    Off,
    On,
    Running,
    Open
}

public enum ClandestineLabVisualLayers : byte
{
    Base,
    Door
}
