namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Broad pathogen family. Currently drives symptoms and contamination; transmission and
/// protection rules are not implemented yet.
/// </summary>
public enum PathogenType : byte
{
    /// <summary>
    /// Viral symptoms and contamination. Proximity spread is not implemented yet.
    /// </summary>
    Virus,

    /// <summary>
    /// Bacterial symptoms and contamination. Contact spread is not implemented yet.
    /// </summary>
    Bacteria,

    /// <summary>
    /// Fungal symptoms and contamination. Environmental-only spread is not implemented yet.
    /// </summary>
    Fungus,
}
