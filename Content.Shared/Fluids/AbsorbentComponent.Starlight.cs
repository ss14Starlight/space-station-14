namespace Content.Shared.Fluids;
public sealed partial class AbsorbentComponent : Component
{

    [DataField]
    public float FootprintCleaningRange = 0.2f;

    /// <summary>
    /// How many footprints within FootprintCleaningRange can be cleaned at once.
    /// </summary>
    [DataField]
    public int MaxCleanedFootprints = 9;
}
