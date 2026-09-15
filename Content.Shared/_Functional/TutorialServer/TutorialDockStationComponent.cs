using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks a tutorial dock station grid (cargo bay, ATS, etc.) so dock/undock goals can require a specific target.
/// Match against <see cref="TutorialSubGoalData.Marker"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialDockStationComponent : Component
{
    /// <summary>
    /// Stable id such as <c>cargo-bay</c> or <c>ats</c>.
    /// </summary>
    [DataField(required: true)]
    public string StationId = string.Empty;
}
