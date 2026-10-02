using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Zones;

[RegisterComponent]
public sealed partial class ZoneMarkerComponent : Component
{
    /// <summary>
    /// Zone prototype ID which will be used to mark room with this zone.
    /// </summary>
    [DataField]
    public ProtoId<ZonePrototype>? Zone;

    /// <summary>
    /// Door prototype this entity was painted as. When set, the door's zones are used instead of <see cref="Zone"/>,
    /// so a painted door keeps every zone of the door it now looks like.
    /// </summary>
    [DataField]
    public EntProtoId? Door;

    /// <summary>
    /// Priority of this marker, if room has another marker with higher priority, this marker will be ignored.
    /// </summary>
    [DataField]
    public int Priority;
}
