using Content.Shared._Starlight.Zones;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Zones;

public sealed partial class ZoneSystem
{
    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Tile)> _boundaryPositions = [];
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, int>> _boundaryTiles = [];

    [SubscribeLocalEvent]
    private void OnBoundaryStartup(Entity<ZoneBoundaryComponent> ent, ref ComponentStartup args)
        => UpdateBoundary(ent);

    [SubscribeLocalEvent]
    private void OnBoundaryAnchorChanged(Entity<ZoneBoundaryComponent> ent, ref AnchorStateChangedEvent args)
        => UpdateBoundary(ent);

    [SubscribeLocalEvent]
    private void OnBoundaryShutdown(Entity<ZoneBoundaryComponent> ent, ref ComponentShutdown args)
        => RemoveBoundary(ent);

    private void UpdateBoundary(EntityUid uid)
    {
        RemoveBoundary(uid);

        var xform = Transform(uid);

        if (!xform.Anchored ||
            xform.GridUid is not { } grid ||
            !_mapGridQuery.TryComp(grid, out var gridComp))
            return;

        var tile = Maps.TileIndicesFor(grid, gridComp, xform.Coordinates);
        _boundaryPositions[uid] = (grid, tile);

        var tiles = _boundaryTiles.GetOrNew(grid);
        tiles[tile] = tiles.GetValueOrDefault(tile) + 1;
        DirtyTile(grid, tile);
    }

    private void RemoveBoundary(EntityUid uid)
    {
        if (!_boundaryPositions.Remove(uid, out var position) ||
            !_boundaryTiles.TryGetValue(position.Grid, out var tiles))
            return;

        if (--tiles[position.Tile] <= 0)
            tiles.Remove(position.Tile);

        if (tiles.Count == 0)
            _boundaryTiles.Remove(position.Grid);

        DirtyTile(position.Grid, position.Tile);
    }

    private bool IsBoundaryTile(EntityUid grid, Vector2i tile)
        => _boundaryTiles.TryGetValue(grid, out var tiles) && tiles.ContainsKey(tile);
}
