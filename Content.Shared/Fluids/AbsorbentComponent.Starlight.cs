namespace Content.Shared.Fluids;

/// <summary>
/// For entities that can clean up puddles
/// </summary>
public sealed partial class AbsorbentComponent : Component
{

    // Funky start - Footprints
    [DataField]
    public float FootprintCleaningRange = 0.2f;

    /// <summary>
    /// How many footprints within FootprintCleaningRange can be cleaned at once.
    /// </summary>
    [DataField]
    public int MaxCleanedFootprints = 9;
    // Funky end
}
