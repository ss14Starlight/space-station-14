// ReSharper disable CheckNamespace
using Content.Shared.Maps;
using Content.Shared.Pinpointer;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Pinpointer;

public sealed partial class NavMapSystem
{
    #region Starlight variables

    private static readonly ProtoId<ContentTileDefinition> SecretFloorId = "FloorSecret";
    private static readonly ProtoId<ContentTileDefinition> SecretFloorLightId = "FloorSecretLight";

    #endregion

    #region Starlight methods

    // Identifies either secret tile variant so both receive the same nav-map wall mask.
    private bool IsSecretFloor(Tile tile)
    {
        if (tile.IsEmpty)
            return false;

        var tileId = _turfSystem.GetContentTileDefinition(tile).ID;
        return tileId == SecretFloorId || tileId == SecretFloorLightId;
    }

    // Refreshes a tile only when its secret-floor status changes, preserving nav-map entity flags.
    private void HandleStarlightTileChanged(ref TileChangedEvent ev, NavMapComponent navMap)
    {
        if (!_gridQuery.TryComp(ev.Entity, out var mapGrid))
            return;

        foreach (var change in ev.Changes)
        {
            if (change.EmptyChanged || IsSecretFloor(change.OldTile) == IsSecretFloor(change.NewTile))
                continue;

            var tile = change.GridIndices;
            var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, ChunkSize);
            var (_, chunk) = RefreshTileEntityContents(ev.Entity, navMap, mapGrid, chunkOrigin, tile, setFloor: true);
            DirtyChunk((ev.Entity, navMap), chunk);
        }
    }

    // Makes secret floors appear as solid walls to nav-map consumers.
    private void ApplyStarlightTileMask(Tile tile, ref int tileData)
    {
        if (IsSecretFloor(tile))
            tileData |= WallMask;
    }

    #endregion
}
