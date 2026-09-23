using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable]
public record struct TwistyCubeEdge(
    TwistyCubeColor Side1,
    TwistyCubeColor Side2
)
{
    public TwistyCubeEdge YX => new(Side2, Side1);
}