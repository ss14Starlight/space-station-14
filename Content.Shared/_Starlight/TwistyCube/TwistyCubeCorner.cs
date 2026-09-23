using System.Text;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable]
public record struct TwistyCubeCorner(
    TwistyCubeColor Side1,
    TwistyCubeColor Side2,
    TwistyCubeColor Side3
)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{Side1}, {Side2}, {Side3}");
        return true;
    }
    // Swizzling
    public TwistyCubeCorner XZY => new(Side1, Side3, Side2);
    public TwistyCubeCorner ZXY => new(Side3, Side1, Side2);
    public TwistyCubeCorner YZX => new(Side2, Side3, Side1);
    public TwistyCubeCorner YXZ => new(Side2, Side1, Side3);
    public TwistyCubeCorner ZYX => new(Side3, Side2, Side1);
}