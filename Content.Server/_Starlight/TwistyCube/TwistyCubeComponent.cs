using Content.Shared._Starlight.TwistyCube;

namespace Content.Server._Starlight.TwistyCube;

[RegisterComponent]
public sealed partial class TwistyCubeComponent : Component
{
    /// The current state of the twisty cube.
    [ViewVariables]
    public TwistyCubeState State { get; private set; } = new();
}
