using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Changeling;

[Serializable, NetSerializable]
public sealed class ChangelingPointingEvent(MapCoordinates target, MapCoordinates origin) : EntityEventArgs
{
    public readonly MapCoordinates Target = target;
    public readonly MapCoordinates Origin = origin;
}
