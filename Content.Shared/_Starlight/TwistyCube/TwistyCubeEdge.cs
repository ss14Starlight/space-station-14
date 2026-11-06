using System.Text;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable, DataRecord]
public partial record struct TwistyCubeEdge(
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Side1,
    [property: ViewVariables(VVAccess.ReadWrite)] TwistyCubeColor Side2
)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{Side1}, {Side2}");
        return true;
    }
    /// <summary>
    /// Returns a copy of this edge with the sides swapped.
    /// </summary>
    public TwistyCubeEdge YX() => new(Side2, Side1);
}