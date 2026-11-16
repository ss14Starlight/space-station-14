// ReSharper disable CheckNamespace
using Content.Shared.Maps;
using Content.Shared.Pinpointer;
using Robust.Shared.Map;

namespace Content.Server.Pinpointer;

public sealed partial class NavMapSystem
{
    private bool IsNavMapWallTile(Tile tile)
        => !tile.IsEmpty && _turfSystem.GetContentTileDefinition(tile).AppearsAsWallOnNavMap;

    private void RefreshNavMapForWallFlagChanges(ref TileChangedEvent ev, NavMapComponent navMap)
    {
        if (!_gridQuery.TryComp(ev.Entity, out var mapGrid))
            return;

        foreach (var change in ev.Changes)
        {
            if (change.EmptyChanged || IsNavMapWallTile(change.OldTile) == IsNavMapWallTile(change.NewTile))
                continue;

            var tile = change.GridIndices;
            var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, ChunkSize);
            var (_, chunk) = RefreshTileEntityContents(ev.Entity, navMap, mapGrid, chunkOrigin, tile, setFloor: true);
            DirtyChunk((ev.Entity, navMap), chunk);
        }
    }

    private void ApplyNavMapWallMask(Tile tile, ref int tileData)
    {
        if (IsNavMapWallTile(tile))
            tileData |= WallMask;
    }
}
