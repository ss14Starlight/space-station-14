using Content.Shared._Starlight.Zones;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Zones;

public sealed partial class ZoneSystem
{
    private static readonly TimeSpan _trackInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedZoneTrackerSystem _tracker = default!;

    private TimeSpan _nextTrack;

    [SubscribeLocalEvent]
    private void OnPlayerAttached(PlayerAttachedEvent args)
        => EnsureComp<ZoneTrackerComponent>(args.Entity);

    private void UpdateTracking()
    {
        if (_timing.CurTime < _nextTrack)
            return;

        _nextTrack = _timing.CurTime + _trackInterval;

        var query = AllEntityQuery<ZoneTrackerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var tracker, out var xform))
            UpdateTracker((uid, tracker), xform);
    }

    private void UpdateTracker(Entity<ZoneTrackerComponent> ent, TransformComponent xform)
    {
        var comp = ent.Comp;

        if (xform.GridUid is not { } grid || !_mapGridQuery.TryComp(grid, out var gridComp))
        {
            _tracker.SetZone(ent, null, default);
            return;
        }

        var tile = Maps.TileIndicesFor(grid, gridComp, xform.Coordinates);
        var revision = _zoneQuery.TryComp(grid, out var zoneComp) ? zoneComp.Revision : 0;

        if (comp.LastPosition == (grid, tile) && comp.LastRevision == revision && comp.Raised == comp.Zone)
            return;

        _tracker.SetZone(ent, GetZone(GetZoneId(grid, tile))?.ID, (grid, tile), revision);
    }
}
