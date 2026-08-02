namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// How serious a strain is. Determines which prevalence budget it draws from, so that
/// background disease can never crowd out an actual outbreak.
/// </summary>
public enum PathogenTier : byte
{
    /// <summary>
    /// Background weather. Harmless, self-clearing, respawns when it dies out.
    /// Yields to anything more serious when the station is getting crowded with illness.
    /// </summary>
    Ambient,

    /// <summary>
    /// Grew out of the station's own neglect once contamination got high enough.
    /// Genuinely unpleasant but survivable, and unlike ambient strains it can be beaten
    /// for good. This is the virologist's real test on a shift with no antagonist.
    /// </summary>
    Emergent,

    /// <summary>
    /// Engineered by someone who meant it. Dangerous, bypasses unsealed protective
    /// equipment, and never respawns once beaten.
    /// </summary>
    Virulent,
}
