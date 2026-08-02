namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Tracks what a host can no longer catch, whether from recovering or from being vaccinated.
///
/// Immunity accumulating across the crew is also what eventually ends an ambient strain:
/// once there is nobody left who can catch it, it stops respawning and dies out. That is
/// herd immunity, and it means a virologist who mass-vaccinates can genuinely eradicate
/// something rather than treating people one at a time forever.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenImmunityComponent : Component
{
    /// <summary>
    /// Strain ids this host is immune to. Ids are per-round, matching the registry.
    /// </summary>
    [DataField]
    public HashSet<int> Immune = new();

    /// <summary>
    /// Immune to everything, always. For anything that has no business catching a disease -
    /// silicons, and anything else we do not want a pathogen to hop onto.
    /// </summary>
    [DataField]
    public bool Total;
}
