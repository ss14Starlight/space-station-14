namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// The broad class of a pathogen.
/// Each type spreads differently and is beaten by a different department, so that an
/// outbreak pulls more of the station in than just the virologist.
/// </summary>
public enum PathogenType : byte
{
    /// <summary>
    /// Spreads fastest and furthest, but each stage hits weakest.
    /// Countered by vaccinating ahead of the front.
    /// </summary>
    Virus,

    /// <summary>
    /// Spreads through completed physical contact. Countered by sterile barriers and
    /// antibiotics, which gives medical chemistry a job.
    /// </summary>
    Bacteria,

    /// <summary>
    /// Environmental only: spreads from rot, biological puddles, and strain-pinned spore
    /// patches, never directly between hosts. Countered by clearing those sources.
    /// </summary>
    Fungus,
}
