namespace Content.Shared._Starlight.TwistyCube;

[RegisterComponent]
public sealed partial class TwistyCubeComponent : Component
{
    public enum TwistyCubeUiKey
    {
        Key
    }
    
    /// The current state of the twisty cube.
    [ViewVariables]
    public TwistyCubeState State { get; private set; } = new();
}