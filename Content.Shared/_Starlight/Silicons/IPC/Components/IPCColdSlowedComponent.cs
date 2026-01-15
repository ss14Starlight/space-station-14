// IPC Cold Slowdown Component
// _STARLIGHT: Original implementation for Starlight
//
// Tracks when an IPC is performing actions slower due to extreme cold.
// Other systems can check this component to apply appropriate delays.

namespace Content.Shared._Starlight.Silicons.IPC.Components;

/// <summary>
/// Marker component indicating that this IPC's actions should be slowed by cold.
/// Applied when temperature reaches maximum cold alert level (260K or below).
/// </summary>
[RegisterComponent]
public sealed partial class IPCColdSlowedComponent : Component
{
    /// <summary>
    /// Multiplier for action delays (1.5 = 50% slower)
    /// </summary>
    [DataField]
    public float SlowdownMultiplier = 1.5f;
}

