using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable]
public enum TwistyCubeAction
{
    // The ordering of these enum variants is important! Do not change!
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
    BackCounterClockwise,
    XClockwise,
    XCounterClockwise,
    YClockwise,
    YCounterClockwise,
    ZClockwise,
    ZCounterClockwise,
    Scramble
}