// IPC Cold Slowdown Component
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

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

