using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Chemistry.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShakeSpillableComponent : Component
{
    /// <summary>
    /// The solution drained by the shake spill path.
    /// </summary>
    [DataField]
    public string SolutionName = "drink";
}
