using Content.Shared._Starlight.Zones;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Zones;

public sealed partial class ZoneSystem
{
    /// <summary>
    /// Every zone currently represented by a room on a grid, including secondary zones of shared rooms.
    /// </summary>
    public HashSet<ProtoId<ZonePrototype>> GetGridZones(EntityUid grid)
    {
        var zones = new HashSet<ProtoId<ZonePrototype>>();
        if (!_zoneQuery.TryComp(grid, out var comp))
            return zones;

        if (comp.NeedsFullRebuild || comp.DirtyTiles.Count > 0 || comp.RenameQueue.Count > 0)
            FullRebuild((grid, comp));

        for (var i = 1; i < comp.RegionCount; i++)
        {
            ref var region = ref comp.Regions[i];
            if (region.Used && region.Alias == NoRegion && region.TileCount > 0)
                zones.UnionWith(GetZones(region.Zone));
        }

        return zones;
    }
}
