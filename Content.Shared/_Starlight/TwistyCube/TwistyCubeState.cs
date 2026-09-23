using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable]
public record struct TwistyCubeState(
    TwistyCubeCorner FrontTopLeft,
    TwistyCubeCorner FrontTopRight,
    TwistyCubeCorner FrontBottomRight,
    TwistyCubeCorner FrontBottomLeft,
    TwistyCubeCorner BackBottomLeft,
    TwistyCubeCorner BackBottomRight,
    TwistyCubeCorner BackTopRight,
    TwistyCubeCorner BackTopLeft,
    TwistyCubeEdge FrontTop,
    TwistyCubeEdge FrontRight,
    TwistyCubeEdge FrontBottom,
    TwistyCubeEdge FrontLeft,
    TwistyCubeEdge TopLeft,
    TwistyCubeEdge TopRight,
    TwistyCubeEdge BottomRight,
    TwistyCubeEdge BottomLeft,
    TwistyCubeEdge BackLeft,
    TwistyCubeEdge BackBottom,
    TwistyCubeEdge BackRight,
    TwistyCubeEdge BackTop
)
{
    public TwistyCubeState() : this(
        new(TwistyCubeColor.Front, TwistyCubeColor.Top, TwistyCubeColor.Left),
        new(TwistyCubeColor.Front, TwistyCubeColor.Top, TwistyCubeColor.Right),
        new(TwistyCubeColor.Front, TwistyCubeColor.Bottom, TwistyCubeColor.Right),
        new(TwistyCubeColor.Front, TwistyCubeColor.Bottom, TwistyCubeColor.Left),
        new(TwistyCubeColor.Back, TwistyCubeColor.Bottom, TwistyCubeColor.Left),
        new(TwistyCubeColor.Back, TwistyCubeColor.Bottom, TwistyCubeColor.Right),
        new(TwistyCubeColor.Back, TwistyCubeColor.Top, TwistyCubeColor.Right),
        new(TwistyCubeColor.Back, TwistyCubeColor.Top, TwistyCubeColor.Left),
        new(TwistyCubeColor.Front, TwistyCubeColor.Top),
        new(TwistyCubeColor.Front, TwistyCubeColor.Right),
        new(TwistyCubeColor.Front, TwistyCubeColor.Bottom),
        new(TwistyCubeColor.Front, TwistyCubeColor.Left),
        new(TwistyCubeColor.Top, TwistyCubeColor.Left),
        new(TwistyCubeColor.Top, TwistyCubeColor.Right),
        new(TwistyCubeColor.Bottom, TwistyCubeColor.Right),
        new(TwistyCubeColor.Bottom, TwistyCubeColor.Left),
        new(TwistyCubeColor.Back, TwistyCubeColor.Left),
        new(TwistyCubeColor.Back, TwistyCubeColor.Bottom),
        new(TwistyCubeColor.Back, TwistyCubeColor.Right),
        new(TwistyCubeColor.Back, TwistyCubeColor.Top)
    )
    { }

    public void ApplyAction(TwistyCubeAction action)
    {
        switch (action)
        {
            case TwistyCubeAction.FrontClockwise:
                (FrontTop, FrontRight, FrontBottom, FrontLeft) = (FrontLeft, FrontTop, FrontRight, FrontBottom);
                (FrontTopLeft, FrontTopRight, FrontBottomRight, FrontBottomLeft)
                    = (FrontBottomLeft.XZY, FrontTopLeft.XZY, FrontTopRight.XZY, FrontBottomRight.XZY);
                break;
            case TwistyCubeAction.FrontCounterClockwise:
                (FrontTop, FrontRight, FrontBottom, FrontLeft) = (FrontRight, FrontBottom, FrontLeft, FrontTop);
                (FrontTopLeft, FrontTopRight, FrontBottomRight, FrontBottomLeft)
                    = (FrontTopRight.XZY, FrontBottomRight.XZY, FrontBottomLeft.XZY, FrontTopLeft.XZY);
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