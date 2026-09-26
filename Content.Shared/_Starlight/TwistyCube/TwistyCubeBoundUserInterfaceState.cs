using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable]
public sealed class TwistyCubeBoundUserInterfaceState(TwistyCubeState compState) : BoundUserInterfaceState
{
    public readonly TwistyCubeState State = compState;
}