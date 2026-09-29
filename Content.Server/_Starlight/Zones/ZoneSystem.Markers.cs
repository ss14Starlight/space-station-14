using Content.Shared._Starlight.Zones;
using Content.Shared.SprayPainter;
using Content.Shared.SprayPainter.Components;

namespace Content.Server._Starlight.Zones;

public sealed partial class ZoneSystem
{
    private static readonly Vector2i[] _markerSpread =
    [
        new(0, 0),
        new(0, 1),
        new(0, -1),
        new(1, 0),
        new(-1, 0),
    ];

    [Dependency] private EntityQuery<ZoneMarkerComponent> _markerQuery = default!;

    #region Events

    [SubscribeLocalEvent]
    private void OnPainted(Entity<PaintableComponent> ent, ref EntityPaintedEvent args)
    {
        var zone = GetDoorZone(args.Prototype.Id);

        if (zone == NoZone && !_markerQuery.HasComp(ent))
            return;

        var marker = EnsureComp<ZoneMarkerComponent>(ent);
        var proto = GetZone(zone);

        if (marker.Zone?.Id == proto?.ID)
            return;

        marker.Zone = proto?.ID;
        DirtyMarkerArea(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnMarkerMapInit(Entity<ZoneMarkerComponent> ent, ref MapInitEvent args)
        => DirtyMarkerArea(ent.Owner);

    [SubscribeLocalEvent]
    private void OnMarkerShutdown(Entity<ZoneMarkerComponent> ent, ref ComponentShutdown args)
        => DirtyMarkerArea(ent.Owner);

    [SubscribeLocalEvent]
    private void OnMarkerAnchorChanged(Entity<ZoneMarkerComponent> ent, ref AnchorStateChangedEvent args)
        => DirtyMarkerArea(ent.Owner);

    private void DirtyMarkerArea(EntityUid uid)
    {
        var xform = Transform(uid);

        if (xform.GridUid is not { } grid || !_mapGridQuery.TryComp(grid, out var gridComp))
            return;

        var tile = Maps.TileIndicesFor(grid, gridComp, xform.Coordinates);

        foreach (var offset in _markerSpread)
            DirtyTile(grid, tile + offset);
    }

    #endregion

    #region Marker layer

    private void RefreshMarkerArea(ZoneContext ctx, ZoneGridComponent comp, Vector2i tile)
    {
        foreach (var offset in _markerSpread)
            RefreshMarker(ctx, comp, tile + offset);
    }

    private void RefreshMarker(ZoneContext ctx, ZoneGridComponent comp, Vector2i tile)
    {
        var (marker, priority) = GetMarkerAt(ctx, tile);
        var chunk = EnsureChunk(comp, tile);
        var index = TileIndex(tile);

        if (chunk.Markers[index] == marker && chunk.MarkerPriorities[index] == priority)
            return;

        chunk.Markers[index] = marker;
        chunk.MarkerPriorities[index] = priority;

        var region = GetRegion(comp, tile);

        if (region != NoRegion)
            QueueRename(comp, region, tile);
    }

    private (ushort Zone, short Priority) GetMarkerAt(ZoneContext ctx, Vector2i tile)
    {
        var best = NoZone;
        var bestPriority = short.MinValue;

        foreach (var offset in _markerSpread)
        {
            var enumerator = Maps.GetAnchoredEntities(ctx.Grid, ctx.GridComp, tile + offset);

            while (enumerator.MoveNext(out var uid))
            {
                if (!TryGetMarkerZone(uid.Value, out var zone, out var priority)
                    || (best != NoZone && priority < bestPriority)
                    || (best != NoZone && priority == bestPriority && CompareZones(zone, best) <= 0))
                    continue;

                best = zone;
                bestPriority = priority;
            }
        }

        return (best, best == NoZone ? (short) 0 : bestPriority);
    }

    private bool TryGetMarkerZone(EntityUid uid, out ushort zone, out short priority)
    {
        if (_markerQuery.TryComp(uid, out var marker))
        {
            zone = marker.Zone is { } id ? GetZoneId(id) : NoZone;
            priority = (short) Math.Clamp(marker.Priority, short.MinValue, short.MaxValue);
            return zone != NoZone;
        }

        zone = GetDoorZone(MetaData(uid).EntityPrototype?.ID);
        priority = 0;
        return zone != NoZone;
    }

    #endregion
}
