using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable]
public enum TwistyCubeColor: byte
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
    /// <summary>
    /// Maps each TwistyCubeColor to some sensible color to represent it.
    /// </summary>
    /// <param name="color">The color of some twisty cube face</param>
    /// <returns>The respective RGB color</returns>
    /// <remarks>This uses a warmer version of the Rubik's Cube color palette.</remarks>
    public static Color AsColor(this TwistyCubeColor color) => (color) switch
    {
        TwistyCubeColor.Right => new Color(0xb1, 0x48, 0x48),
        TwistyCubeColor.Back => new Color(0x83, 0x85, 0xcf),
        TwistyCubeColor.Left => new Color(0xfc, 0x9e, 0x6a),
        TwistyCubeColor.Front => new Color(0xbb, 0xc8, 0x40),
        TwistyCubeColor.Top => new Color(0xed, 0xe6, 0xc8),
        TwistyCubeColor.Bottom => new Color(0xf1, 0xde, 0x72),
        _ => Color.Magenta
    };
}