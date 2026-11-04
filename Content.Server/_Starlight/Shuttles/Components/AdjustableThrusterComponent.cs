using Content.Server.Shuttles.Systems;

namespace Content.Server._Starlight.Shuttles.Components;

/// <summary>
/// Lets a thruster's thrust output be retuned through its maintenance panel.
/// </summary>
[RegisterComponent, Access(typeof(ThrusterSystem))]
public sealed partial class AdjustableThrusterComponent : Component
{
    /// <summary>
    /// The thrust the thruster was mapped with, which the upper tuning bound is relative to.
    /// Captured on map init when left unset.
    /// </summary>
    [DataField]
    public float BaseThrust;

    /// <summary>
    /// Lowest tuning the panel allows.
    /// </summary>
    [DataField]
    public float MinThrust = 1f;

    /// <summary>
    /// Highest tuning the panel allows, as a fraction of <see cref="BaseThrust"/>.
    /// </summary>
    [DataField]
    public float MaxMultiplier = 1f;
}
