using Robust.Shared.Serialization;
namespace Content.Shared._Starlight.TwistyCube;
[Serializable, NetSerializable, DataRecord]
public partial record struct TwistyCubeState(
    // Okay this is implemented pretty weirdly.
    // Each corner has 3 fields, corresponding to its 3 colors.
    // Each edge has 2 fields, corresponding to its 2 colors.
    // The name of the field denotes which side corresponds to which color.
    // For example, FrontTopLeft has its Side1 on the Front, Side2 on the Top, and Side3 on the Left.
    // FrontTop has its Side1 on the Front and its Side2 on the Top.
    // Cube turning is implemented by swizzling and swapping the values of these fields.
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeCorner FrontTopLeft,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeCorner FrontTopRight,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeCorner FrontBottomRight,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeCorner FrontBottomLeft,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeCorner BackBottomLeft,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeCorner BackBottomRight,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeCorner BackTopRight,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeCorner BackTopLeft,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge FrontTop,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge FrontRight,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge FrontBottom,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge FrontLeft,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge TopLeft,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge TopRight,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge BottomRight,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge BottomLeft,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge BackLeft,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge BackBottom,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge BackRight,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeEdge BackTop,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Top,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Left,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Front,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Bottom,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Right,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Back
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
        new(TwistyCubeColor.Back, TwistyCubeColor.Top),
        TwistyCubeColor.Top,
        TwistyCubeColor.Left,
        TwistyCubeColor.Front,
        TwistyCubeColor.Bottom,
        TwistyCubeColor.Right,
        TwistyCubeColor.Back
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
            case TwistyCubeAction.SClockwise:
                (Top, Right, Bottom, Left) = (Left, Top, Right, Bottom);
                (TopLeft, TopRight, BottomRight, BottomLeft) = (BottomLeft.YX(), TopLeft.YX(), TopRight.YX(), BottomRight.YX());
                break;
            case TwistyCubeAction.MClockwise:
                (Top, Back, Bottom, Front) = (Back, Bottom, Front, Top);
                (FrontTop, BackTop, BackBottom, FrontBottom) = (BackTop.YX(), BackBottom.YX(), FrontBottom.YX(), FrontTop.YX());
                break;
            case TwistyCubeAction.EClockwise:
                (Front, Right, Back, Left) = (Right, Back, Left, Front);
                (FrontLeft, BackLeft, BackRight, FrontRight) = (FrontRight.YX(), FrontLeft.YX(), BackLeft.YX(), BackRight.YX());
                break;
            case TwistyCubeAction.XClockwise:
                ApplyAction(TwistyCubeAction.LeftClockwise);
                ApplyAction(TwistyCubeAction.RightCounterClockwise);
                ApplyAction(TwistyCubeAction.MClockwise);
                break;
            case TwistyCubeAction.YClockwise:
                ApplyAction(TwistyCubeAction.TopClockwise);
                ApplyAction(TwistyCubeAction.BottomCounterClockwise);
                ApplyAction(TwistyCubeAction.EClockwise);
                break;
            case TwistyCubeAction.ZClockwise:
                ApplyAction(TwistyCubeAction.FrontClockwise);
                ApplyAction(TwistyCubeAction.BackCounterClockwise);
                ApplyAction(TwistyCubeAction.SClockwise);
                break;
            case TwistyCubeAction.FrontCounterClockwise:
            case TwistyCubeAction.LeftCounterClockwise:
            case TwistyCubeAction.TopCounterClockwise:
            case TwistyCubeAction.RightCounterClockwise:
            case TwistyCubeAction.BottomCounterClockwise:
            case TwistyCubeAction.BackCounterClockwise:
            case TwistyCubeAction.SCounterClockwise:
            case TwistyCubeAction.MCounterClockwise:
            case TwistyCubeAction.ECounterClockwise:
            case TwistyCubeAction.XCounterClockwise:
            case TwistyCubeAction.YCounterClockwise:
            case TwistyCubeAction.ZCounterClockwise:
                for (int i = 0; i < 3; i++) ApplyAction(action - 1);
                break;
            default:  return;
        }
    }
    public bool Solved =>
        Front == FrontTop.Side1 &&
        Front == FrontBottom.Side1 &&
        Front == FrontLeft.Side1 &&
        Front == FrontRight.Side1 &&
        Front == FrontTopLeft.Side1 &&
        Front == FrontTopRight.Side1 &&
        Front == FrontBottomRight.Side1 &&
        Front == FrontBottomLeft.Side1 &&
        Back == BackTop.Side1 &&
        Back == BackBottom.Side1 &&
        Back == BackLeft.Side1 &&
        Back == BackRight.Side1 &&
        Back == BackTopLeft.Side1 &&
        Back == BackTopRight.Side1 &&
        Back == BackBottomRight.Side1 &&
        Back == BackBottomLeft.Side1 &&
        Top == FrontTop.Side2 &&
        Top == BackTop.Side2 &&
        Top == TopLeft.Side1 &&
        Top == TopRight.Side1 &&
        Top == FrontTopLeft.Side2 &&
        Top == FrontTopRight.Side2 &&
        Top == BackTopRight.Side2 &&
        Top == BackTopLeft.Side2 &&
        Bottom == FrontBottom.Side2 &&
        Bottom == BackBottom.Side2 &&
        Bottom == BottomLeft.Side1 &&
        Bottom == BottomRight.Side1 &&
        Bottom == FrontBottomLeft.Side2 &&
        Bottom == FrontBottomRight.Side2 &&
        Bottom == BackBottomRight.Side2 &&
        Bottom == BackBottomLeft.Side2 &&
        Left == FrontLeft.Side2 &&
        Left == BackLeft.Side2 &&
        Left == TopLeft.Side2 &&
        Left == BottomLeft.Side2 &&
        Left == FrontTopLeft.Side3 &&
        Left == FrontBottomLeft.Side3 &&
        Left == BackTopLeft.Side3 &&
        Left == BackBottomLeft.Side3 &&
        Right == FrontRight.Side2 &&
        Right == BackRight.Side2 &&
        Right == TopRight.Side2 &&
        Right == BottomRight.Side2 &&
        Right == FrontTopRight.Side3 &&
        Right == FrontBottomRight.Side3 &&
        Right == BackTopRight.Side3 &&
        Right == BackBottomRight.Side3;
}
