using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable]
public enum TwistyCubeAction
{
    FrontClockwise,
    FrontCounterClockwise,
    LeftClockwise,
    LeftCounterClockwise,
    TopClockwise,
    TopCounterClockwise,
    RightClockwise,
    RightCounterClockwise,
    BottomClockwise,
    BottomCounterClockwise,
    BackClockwise,
    BackCounterClockwise
}