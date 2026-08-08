namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Broad transmission family. Determines spread, symptoms, protection and contamination.
/// </summary>
public enum PathogenType : byte
{
    /// <summary>
    /// Spreads through proximity.
    /// </summary>
    Virus,

    /// <summary>
    /// Spreads through physical contact only.
    /// </summary>
    Bacteria,

    /// <summary>
    /// Spreads from environmental sources only, never directly between hosts.
    /// </summary>
    Fungus,
}
