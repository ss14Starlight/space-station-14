using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Zones;

[Prototype]
public sealed partial class ZonePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public Color Color = Color.White;

    [DataField(required: true)]
    public int Priority;

    [DataField]
    public List<EntProtoId> Doors = [];
}
