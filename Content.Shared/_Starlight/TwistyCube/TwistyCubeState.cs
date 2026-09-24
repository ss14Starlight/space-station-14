using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable, DataRecord]
public partial record struct TwistyCubeState(
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeCorner FrontTopLeft,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeCorner FrontTopRight,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeCorner FrontBottomRight,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeCorner FrontBottomLeft,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeCorner BackBottomLeft,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeCorner BackBottomRight,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeCorner BackTopRight,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeCorner BackTopLeft,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge FrontTop,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge FrontRight,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge FrontBottom,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge FrontLeft,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge TopLeft,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge TopRight,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge BottomRight,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge BottomLeft,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge BackLeft,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge BackBottom,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge BackRight,
    [field: ViewVariables(VVAccess.ReadOnly)] TwistyCubeEdge BackTop
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
    
    /// <summary>
    /// Performs a given face turn on the state of the cube.
    /// </summary>
    /// <param name="action">The face turn to perform</param>
    public void ApplyAction(TwistyCubeAction action)
    {
        switch (action)
        {
            case TwistyCubeAction.FrontClockwise:
                (FrontTop, FrontRight, FrontBottom, FrontLeft) = (FrontLeft, FrontTop, FrontRight, FrontBottom);
                (FrontTopLeft, FrontTopRight, FrontBottomRight, FrontBottomLeft)
                    = (FrontBottomLeft.XZY(), FrontTopLeft.XZY(), FrontTopRight.XZY(), FrontBottomRight.XZY());
                break;
            case TwistyCubeAction.LeftClockwise:
                (FrontLeft, TopLeft, BackLeft, BottomLeft) = (TopLeft, BackLeft, BottomLeft, FrontLeft);
                (FrontBottomLeft, BackBottomLeft, BackTopLeft, FrontTopLeft)
                    = (FrontTopLeft.YXZ(), FrontBottomLeft.YXZ(), BackBottomLeft.YXZ(), BackTopLeft.YXZ());
                break;
            case TwistyCubeAction.TopClockwise:
                (BackTop, TopRight, FrontTop, TopLeft) = (TopLeft.YX(), BackTop.YX(), TopRight.YX(), FrontTop.YX());
                (BackTopLeft, BackTopRight, FrontTopRight, FrontTopLeft)
                    = (FrontTopLeft.ZYX(), BackTopLeft.ZYX(), BackTopRight.ZYX(), FrontTopRight.ZYX());
                break;
            case TwistyCubeAction.RightClockwise:
                (FrontRight, TopRight, BackRight, BottomRight) = (BottomRight, FrontRight, TopRight, BackRight);
                (FrontBottomRight, BackBottomRight, BackTopRight, FrontTopRight)
                    = (BackBottomRight.YXZ(), BackTopRight.YXZ(), FrontTopRight.YXZ(), FrontBottomRight.YXZ());
                break;
            case TwistyCubeAction.BottomClockwise:
                (FrontBottom, BottomLeft, BackBottom, BottomRight) = (BottomLeft.YX(), BackBottom.YX(), BottomRight.YX(), FrontBottom.YX());
                (FrontBottomRight, FrontBottomLeft, BackBottomLeft, BackBottomRight)
                    = (FrontBottomLeft.ZYX(), BackBottomLeft.ZYX(), BackBottomRight.ZYX(), FrontBottomRight.ZYX());
                break;
            case TwistyCubeAction.BackClockwise:
                (BackBottom, BackLeft, BackTop, BackRight) = (BackLeft, BackTop, BackRight, BackBottom);
                (BackTopLeft, BackTopRight, BackBottomRight, BackBottomLeft)
                    = (BackTopRight.XZY(), BackBottomRight.XZY(), BackBottomLeft.XZY(), BackTopLeft.XZY());
                break;
            case TwistyCubeAction.FrontCounterClockwise:
            case TwistyCubeAction.LeftCounterClockwise:
            case TwistyCubeAction.TopCounterClockwise:
            case TwistyCubeAction.RightCounterClockwise:
            case TwistyCubeAction.BottomCounterClockwise:
            case TwistyCubeAction.BackCounterClockwise:
                for (int i = 0; i < 3; i++) ApplyAction(action - 1);
                break;
            default:  return;
        }
    }
}