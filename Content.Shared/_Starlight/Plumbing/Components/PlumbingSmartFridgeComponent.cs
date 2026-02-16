using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Plumbing.Components;

/// <summary>
/// A plumbing-connected smart fridge that stores reagents pulled from the network
/// and fills labeled jugs on interaction.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlumbingSmartFridgeComponent : Component
{
    /// <summary>
    /// The solution container name for the fridge's reagent storage.
    /// </summary>
    [DataField]
    public string SolutionName = "fridge";

    /// <summary>
    /// Maximum amount of any single reagent the fridge can store.
    /// </summary>
    [DataField]
    public FixedPoint2 MaxPerReagent = FixedPoint2.New(200);
}
