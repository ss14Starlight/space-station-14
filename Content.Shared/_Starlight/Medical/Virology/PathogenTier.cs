namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// How serious a strain is. Determines which prevalence budget it draws from.
/// </summary>
public enum PathogenTier : byte
{
    /// <summary>
    /// Harmless and self-clearing. Eligible for extinction respawn when that gate is enabled.
    /// </summary>
    Ambient,

    /// <summary>
    /// Survivable escalation tier. Future milestone seeding creates these from station
    /// contamination; they do not respawn once cured.
    /// </summary>
    Emergent,

    /// <summary>
    /// Deliberately engineered.
    /// </summary>
    Virulent,
}
