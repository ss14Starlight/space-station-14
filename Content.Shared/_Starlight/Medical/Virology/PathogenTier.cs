namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// How serious a strain is. Determines which prevalence budget it draws from.
/// </summary>
public enum PathogenTier : byte
{
    /// <summary>
    /// Harmless and self-clearing. Respawns after it dies out.
    /// </summary>
    Ambient,

    /// <summary>
    /// Seeded when station contamination crosses a milestone. Survivable, and does not
    /// respawn once cured.
    /// </summary>
    Emergent,

    /// <summary>
    /// Deliberately engineered.
    /// </summary>
    Virulent,
}
