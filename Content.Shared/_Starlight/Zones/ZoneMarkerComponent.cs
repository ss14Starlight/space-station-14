using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Zones;

[RegisterComponent]
public sealed partial class ZoneMarkerComponent : Component
{
    [DataField]
    public ProtoId<ZonePrototype>? Zone;

    [DataField]
    public int Priority;
}
