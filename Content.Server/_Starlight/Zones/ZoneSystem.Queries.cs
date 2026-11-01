using System.Linq;
using Content.Shared._Starlight.Zones;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Zones;

public sealed partial class ZoneSystem
{
    /// <summary>
    /// Every zone currently represented by a room on a grid, including secondary zones of shared rooms.
    /// Initializes topology on the first query if needed. Later edits are processed by the normal tile budget.
    /// </summary>
    public IReadOnlyList<ProtoId<ZonePrototype>> GetGridZones(EntityUid grid)
    {
        if (!_zoneQuery.TryComp(grid, out var comp))
            return [];

        if (!comp.TopologyInitialized)
        {
            // Round-start queries can query zones before the first zone-system update.
            FullRebuild((grid, comp));
            comp.Revision++;

            if (!comp.TopologyInitialized)
                return [];
        }

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
