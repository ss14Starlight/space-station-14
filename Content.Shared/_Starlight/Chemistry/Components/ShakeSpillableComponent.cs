using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Chemistry.Components;

// Starlight-start: Bottles spill without cap when shaken
[RegisterComponent, NetworkedComponent]
public sealed partial class ShakeSpillableComponent : Component
{
    /// <summary>
    /// The solution drained by the shake spill path.
    /// </summary>
    [DataField]
    public string SolutionName = "drink";
}
// Starlight-end
