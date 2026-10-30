using Content.Shared._Funkystation.Footprints;

namespace Content.Shared.Fluids;
public sealed partial class AbsorbentComponent : Component
{
    /// <summary>
    /// Which footprint tiles are cleaned when this absorbent is used on a footprint.
    /// </summary>
    [DataField]
    public FootprintCleaningPattern FootprintCleaning = FootprintCleaningPattern.Target;

    /// <summary>
    /// Whether cleaning a wall also cleans stained walls on the eight surrounding tiles.
    /// </summary>
    [DataField]
    public bool CleanAdjacentWallStains;
}
