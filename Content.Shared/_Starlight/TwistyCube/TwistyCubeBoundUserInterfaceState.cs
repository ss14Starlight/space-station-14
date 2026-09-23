namespace Content.Shared._Starlight.TwistyCube;

public sealed class TwistyCubeBoundUserInterfaceState(TwistyCubeState compState) : BoundUserInterfaceState
{
    public readonly TwistyCubeState State = compState;
}