using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Movement.Pulling.Components;

/// <summary>
/// Lets this entity couple further pullables onto the end of what it already pulls, forming a train.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PullTrainComponent : Component
{
    /// <summary>
    /// How many entities may be coupled behind this one.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxCars = 3;
}
