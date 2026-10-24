using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.MindCollective;

[Serializable, NetSerializable]
public sealed class MindCollectivePointingEvent(MapCoordinates target, MapCoordinates origin) : EntityEventArgs
{
    public readonly MapCoordinates Target = target;
    public readonly MapCoordinates Origin = origin;
}
