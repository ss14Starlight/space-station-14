using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

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

// The cube is laid out with:
// Red on front +Y
// Blue on right +X
// White on top +Z
// Orange on back -Y
// Green on left -X
// Yellow on bottom -Z

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