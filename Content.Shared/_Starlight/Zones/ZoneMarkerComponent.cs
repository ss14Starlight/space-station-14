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
    /// Priority of this marker, if room has another marker with higher priority, this marker will be ignored.
    /// </summary>
    [DataField]
    public int Priority;
}
