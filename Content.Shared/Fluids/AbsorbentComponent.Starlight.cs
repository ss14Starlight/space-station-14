using Content.Shared._Funkystation.Footprints;

namespace Content.Shared.Fluids;
public sealed partial class AbsorbentComponent : Component
{
    /// <summary>
    /// Which footprint tiles are cleaned when this absorbent is used on a footprint.
    /// </summary>
    [DataField]
    public FootprintCleaningPattern FootprintCleaning = FootprintCleaningPattern.Target;
}
