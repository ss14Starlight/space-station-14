using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Scent.Events;

/// <summary>
/// Sent when a Full smeller starts tracking a scent. Only sent if the source still exists.
/// </summary>
[Serializable, NetSerializable]
public sealed class ScentSourcePingEvent(string scentId, MapId mapId, Vector2 position) : EntityEventArgs
{
    public string ScentId = scentId;
    public MapId MapId = mapId;
    public Vector2 Position = position;
}
