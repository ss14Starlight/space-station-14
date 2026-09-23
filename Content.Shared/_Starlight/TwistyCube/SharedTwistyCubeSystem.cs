using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

public abstract partial class SharedTwistyCubeSystem : EntitySystem
{ }

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
    BackCounterClockwise,
    RequestData,
}

// The cube is laid out with:
// Red on front +Y
// Blue on right +X
// White on top +Z
// Orange on back -Y
// Green on left -X
// Yellow on bottom -Z

[Serializable, NetSerializable]
public enum TwistyCubeColor
{
    Front,
    Back,
    Left,
    Right,
    Top,
    Bottom
}

[Serializable, NetSerializable]
public record struct TwistyCubeCorner(
    TwistyCubeColor Side1,
    TwistyCubeColor Side2,
    TwistyCubeColor Side3
)
{
    // Swizzling
    public TwistyCubeCorner XZY => new(Side1, Side3, Side2);
    public TwistyCubeCorner ZXY => new(Side3, Side1, Side2);
    public TwistyCubeCorner YZX => new(Side2, Side3, Side1);
    public TwistyCubeCorner YXZ => new(Side2, Side1, Side3);
    public TwistyCubeCorner ZYX => new(Side3, Side2, Side1);
}

[Serializable, NetSerializable]
public record struct TwistyCubeEdge(
    TwistyCubeColor Side1,
    TwistyCubeColor Side2
)
{
    public TwistyCubeEdge YX => new(Side2, Side1);
}

[Serializable, NetSerializable]
public record struct TwistyCubeState(
    TwistyCubeCorner TopLeftFront,
    TwistyCubeCorner TopRightFront,
    TwistyCubeCorner BottomRightFront,
    TwistyCubeCorner BottomLeftFront,
    TwistyCubeCorner BottomLeftBack,
    TwistyCubeCorner BottomRightBack,
    TwistyCubeCorner TopRightBack,
    TwistyCubeCorner TopLeftBack,
    TwistyCubeEdge FrontTop,
    TwistyCubeEdge FrontRight,
    TwistyCubeEdge FrontBottom,
    TwistyCubeEdge FrontLeft,
    TwistyCubeEdge BackLeft,
    TwistyCubeEdge BackBottom,
    TwistyCubeEdge BackRight,
    TwistyCubeEdge BackTop
)
{
    public void ApplyAction(TwistyCubeAction action)
    {
        switch (action)
        {
            case TwistyCubeAction.FrontClockwise:
                (FrontTop, FrontRight, FrontBottom, FrontLeft) = (FrontLeft, FrontTop, FrontRight, FrontBottom);
                (TopLeftFront, TopRightFront, BottomRightFront, BottomLeftFront)
                    = (BottomLeftFront.YXZ, TopLeftFront.YXZ, TopRightFront.YXZ, BottomRightFront.YXZ);
                break;
            case TwistyCubeAction.FrontCounterClockwise:
                (FrontTop, FrontRight, FrontBottom, FrontLeft) = (FrontRight, FrontBottom, FrontLeft, FrontTop);
                (TopLeftFront, TopRightFront, BottomRightFront, BottomLeftFront)
                    = (TopRightFront.YXZ, BottomRightFront.YXZ, BottomLeftFront.YXZ, TopLeftFront.YXZ);
                break;
            case TwistyCubeAction.LeftClockwise:
                break;
            case TwistyCubeAction.LeftCounterClockwise:
                break;
            case TwistyCubeAction.TopClockwise:
                break;
            case TwistyCubeAction.TopCounterClockwise:
                break;
            case TwistyCubeAction.RightClockwise:
                break;
            case TwistyCubeAction.RightCounterClockwise:
                break;
            case TwistyCubeAction.BottomClockwise:
                break;
            case TwistyCubeAction.BottomCounterClockwise:
                break;
            case TwistyCubeAction.BackClockwise:
                break;
            case TwistyCubeAction.BackCounterClockwise:
                break;
            default:  return;
        }
    }
}
