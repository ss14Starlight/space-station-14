using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Mindshield.Components;

/// <summary>
/// Component that tracks mindshield degradation for head revolutionaries.
/// Allows them to appear mindshielded to others while their protection degrades over time.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MindshieldDegradationComponent : Component
{
    /// <summary>
    /// When the degradation process started
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan StartTime;

    /// <summary>
    /// Total time for the mindshield to completely degrade (10 minutes)
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan DegradationTime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Time when the warning message should be shown (5 minutes)
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan WarningTime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether the warning message has been shown
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool WarningShown = false;

    /// <summary>
    /// Whether the degradation process is complete
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool DegradationComplete = false;
}
