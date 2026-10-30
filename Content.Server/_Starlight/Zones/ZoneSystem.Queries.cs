using System.Linq;
using Content.Shared._Starlight.Zones;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Zones;

public sealed partial class ZoneSystem
{
    /// <summary>
    /// Every zone currently represented by a room on a grid, including secondary zones of shared rooms.
    /// Returns no zones until the first scheduled rebuild. Pending edits are processed by the normal tile budget.
    /// </summary>
    public IReadOnlyList<ProtoId<ZonePrototype>> GetGridZones(EntityUid grid)
    {
        if (!_zoneQuery.TryComp(grid, out var comp) || !comp.TopologyInitialized)
            return [];

        if (comp.CachedGridZones != null)
            return comp.CachedGridZones;

        var zones = new HashSet<ProtoId<ZonePrototype>>();
        for (var i = 1; i < comp.RegionCount; i++)
        {
            ref var region = ref comp.Regions[i];
            if (region.Used && region.Alias == NoRegion && region.TileCount > 0)
                zones.UnionWith(GetZones(region.Zone));
        }

        return comp.CachedGridZones = zones.ToArray();
    }
}
