using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Quarantine alarm control button with separate lock and switch-state indicators.
/// </summary>
[RegisterComponent]
public sealed partial class VirologyAlarmButtonComponent : Component;

[Serializable, NetSerializable]
public enum VirologyAlarmButtonVisuals : byte
{
    On
}

public enum VirologyAlarmButtonVisualLayers : byte
{
    Indicator
}
