using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Zones;

[Prototype]
public sealed partial class ZonePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId? Name;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public int Priority;

    [DataField]
    public List<EntProtoId> Doors = new();
}
