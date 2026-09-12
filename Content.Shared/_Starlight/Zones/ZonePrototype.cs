using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Zones;

[Prototype]
public sealed partial class ZonePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Color of zone which will be used only in Zone/Rooms overlay.
    /// </summary>
    [DataField(required: true)]
    public Color Color = Color.White;

    /// <summary>
    /// Determines priority of this zone, it means that zone with higher priority will "eat" zone with smaller priority on merge and another such situations.
    /// </summary>
    [DataField(required: true)]
    public int Priority;

    /// <summary>
    /// List of doors which will be used to mark room with this zone.
    /// </summary>
    [DataField]
    public List<EntProtoId> Doors = [];
}
