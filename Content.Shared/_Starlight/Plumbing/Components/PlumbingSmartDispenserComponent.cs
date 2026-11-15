using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Plumbing.Components;

/// <summary>
/// A plumbing-connected smart dispenser that stores reagents pulled from the network,
/// supports held container dispensing and label matching dispensing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PlumbingSmartDispenserComponent : Component
{
    /// <summary>
    /// The solution container name for the dispenser's reagent storage.
    /// </summary>
    [DataField]
    public string SolutionName = "fridge";

    /// <summary>
    /// Maximum amount of any single reagent the dispenser can store.
    /// </summary>
    [DataField]
    public FixedPoint2 MaxPerReagent = FixedPoint2.New(200);

    /// <summary>
    /// Map from reagent prototype to custom label set with the labeller
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, string> ReagentLabels = [];
}
