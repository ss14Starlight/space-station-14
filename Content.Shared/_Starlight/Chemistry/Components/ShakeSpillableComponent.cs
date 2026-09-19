using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Chemistry.Components;

// Starlight-start: Bottles spill without cap when shaken
[RegisterComponent, NetworkedComponent]
public sealed partial class ShakeSpillableComponent : Component
{
    [DataField]
    public string SolutionName = "drink";
}
// Starlight-end
