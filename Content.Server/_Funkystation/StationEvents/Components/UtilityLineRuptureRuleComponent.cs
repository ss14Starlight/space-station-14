using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared.FixedPoint;

namespace Content.Server._Funkystation.StationEvents.Components;

/// <summary>
/// This event announces a utility line rupture and then spills and ignites a large quantity of flammable reagent after a delay
/// </summary>
[RegisterComponent, Access(typeof(Events.UtilityLineRuptureRule))]
public sealed partial class UtilityLineRuptureRuleComponent : Component
{
    /// <summary>
    /// When the actual spill and fire should occur
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? RuptureTime;

    /// <summary>
    /// The location where the fire will happen
    /// </summary>
    public EntityCoordinates? TargetCoordinates;

    /// <summary>
    /// Volume of reagent to spill
    /// </summary>
    [DataField]
    public FixedPoint2 SpillVolume = FixedPoint2.New(1000);

    /// <summary>
    /// List of possible flammable reagents to be spilled
    /// </summary>
    [DataField]
    public List<string> PossibleReagents = ["WeldingFuel", "Acetone", "Phlogiston"];

    /// <summary>
    /// The chosen flammable reagent that will be spilled and ignited. Selected at event start.
    /// </summary>
    [DataField]
    public string Reagent = "WeldingFuel";
}
