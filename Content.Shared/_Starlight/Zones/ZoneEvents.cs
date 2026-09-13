using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Zones;

[ByRefEvent]
public readonly record struct ZoneChangedEvent(
    ProtoId<ZonePrototype>? Old,
    ProtoId<ZonePrototype>? New);

[Serializable, NetSerializable]
public sealed class RequestZoneShapesEvent(NetEntity grid) : EntityEventArgs
{
    public NetEntity Grid = grid;
}

[Serializable, NetSerializable]
public sealed class ZoneShapesSyncEvent(NetEntity grid, List<ZoneShapeSet> shapes) : EntityEventArgs
{
    public NetEntity Grid = grid;
    public List<ZoneShapeSet> Shapes = shapes;
}

[Serializable, NetSerializable]
public sealed class RequestZoneRectEvent(NetEntity grid, Box2i rect, ProtoId<ZonePrototype> zone) : EntityEventArgs
{
    public NetEntity Grid = grid;
    public Box2i Rect = rect;
    public ProtoId<ZonePrototype> Zone = zone;
}

[Serializable, NetSerializable]
public sealed class RequestZoneEraseEvent(NetEntity grid, Box2i rect) : EntityEventArgs
{
    public NetEntity Grid = grid;
    public Box2i Rect = rect;
}

[Serializable, NetSerializable]
public sealed class RequestZoneRoomsEvent(NetEntity grid, Vector2i centre) : EntityEventArgs
{
    public NetEntity Grid = grid;
    public Vector2i Centre = centre;
}

[Serializable, NetSerializable]
public sealed class ZoneRoomsSyncEvent(NetEntity grid, List<ZoneRoomChunk> chunks, Dictionary<ushort, ProtoId<ZonePrototype>> zones)
    : EntityEventArgs
{
    public NetEntity Grid = grid;

    public List<ZoneRoomChunk> Chunks = chunks;

    public Dictionary<ushort, ProtoId<ZonePrototype>> Zones = zones;
}

[Serializable, NetSerializable]
public sealed class ZoneRoomChunk(Vector2i origin, ushort[] rooms)
{
    public Vector2i Origin = origin;
    public ushort[] Rooms = rooms;
}
