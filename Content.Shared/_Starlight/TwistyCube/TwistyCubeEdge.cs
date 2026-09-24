using System.Text;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable]
public record struct TwistyCubeEdge(
    TwistyCubeColor Side1,
    TwistyCubeColor Side2
)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{Side1}, {Side2}");
        return true;
    }
    public TwistyCubeEdge YX() => new(Side2, Side1);
}