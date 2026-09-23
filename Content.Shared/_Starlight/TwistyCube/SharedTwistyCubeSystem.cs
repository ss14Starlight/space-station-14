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
    RequestData
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

public static class Extensions
{
    public static Color AsColor(this TwistyCubeColor color) => (color) switch
    {
        TwistyCubeColor.Front => new Color(0xFF, 0x20, 0x20),
        TwistyCubeColor.Back => new Color(0xFF, 0x90, 0x20),
        TwistyCubeColor.Left => new Color(0x20, 0xFF, 0x20),
        TwistyCubeColor.Right => new Color(0x20, 0x20, 0xFF),
        TwistyCubeColor.Top => new Color(0xFF, 0xFF, 0xFF),
        TwistyCubeColor.Bottom => new Color(0xFF, 0xFF, 0x20),
        _ => Color.Magenta
    };
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
