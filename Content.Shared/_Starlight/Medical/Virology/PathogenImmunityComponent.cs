namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Tracks what a host can no longer catch, from recovery or from vaccination.
/// Accumulated immunity is also what retires an ambient strain: once nobody left can catch
/// it, it stops respawning.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenImmunityComponent : Component
{
    /// <summary>
    /// Strain ids this host is immune to.
    /// </summary>
    [DataField]
    public HashSet<int> Immune = new();

    /// <summary>
    /// Immune to everything, permanently. For hosts that should never be infected, such as
    /// synthetics and the undead.
    /// </summary>
    [DataField]
    public bool Total;
}
