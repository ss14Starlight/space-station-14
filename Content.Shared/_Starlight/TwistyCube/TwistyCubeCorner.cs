using System.Text;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable, DataRecord]
public partial record struct TwistyCubeCorner(
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Side1,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Side2,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Side3
)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{Side1}, {Side2}, {Side3}");
        return true;
    }
    /// <summary>
    /// Returns a copy of this corner with the second and third sides swapped.
    /// </summary>
    public TwistyCubeCorner XZY() => new(Side1, Side3, Side2);
    /// <summary>
    /// Returns a copy of this corner with all sides rotated forwards by one field.
    /// </summary>
    public TwistyCubeCorner ZXY() => new(Side3, Side1, Side2);
    /// <summary>
    /// Returns a copy of this corner with all sides rotated backwards by one field.
    /// </summary>
    public TwistyCubeCorner YZX() => new(Side2, Side3, Side1);
    /// <summary>
    /// Returns a copy of this corner with the first and second sides swapped.
    /// </summary>
    public TwistyCubeCorner YXZ() => new(Side2, Side1, Side3);
    /// <summary>
    /// Returns a copy of this corner with the first and third sides swapped.
    /// </summary>
    public TwistyCubeCorner ZYX() => new(Side3, Side2, Side1);
}