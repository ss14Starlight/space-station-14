using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.TwistyCube;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TwistyCubeComponent : Component
{
    /// The current state of the twisty cube.
    [DataField]
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public TwistyCubeState State = new();
}