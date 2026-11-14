// ReSharper disable CheckNamespace

using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem
{
    public void ReserveLandingTiles(
        EntityUid mapUid,
        Box2 bounds,
        List<(Vector2i Index, Tile Tile)> generatedTiles,
        List<Vector2i> newlyModifiedTiles,
        BiomeComponent? biome = null,
        MapGridComponent? mapGrid = null)
    {
        generatedTiles.Clear();
        newlyModifiedTiles.Clear();

        if (!Resolve(mapUid, ref biome, ref mapGrid, false))
            return;

        var previouslyModified = new HashSet<Vector2i>();
        foreach (var tile in _mapSystem.GetLocalTilesIntersecting(mapUid, mapGrid, bounds, false))
        {
            var index = tile.GridIndices;
            var chunkOrigin = SharedMapSystem.GetChunkIndices(index, ChunkSize) * ChunkSize;
            if (biome.ModifiedTiles.TryGetValue(chunkOrigin, out var modified) && modified.Contains(index))
                previouslyModified.Add(index);
        }

        ReserveTiles(mapUid, bounds, generatedTiles, biome, mapGrid);

        foreach (var tile in _mapSystem.GetLocalTilesIntersecting(mapUid, mapGrid, bounds, false))
        {
            var index = tile.GridIndices;
            if (previouslyModified.Contains(index))
                continue;

            var chunkOrigin = SharedMapSystem.GetChunkIndices(index, ChunkSize) * ChunkSize;
            if (biome.ModifiedTiles.TryGetValue(chunkOrigin, out var modified) && modified.Contains(index))
                newlyModifiedTiles.Add(index);
        }
    }

    public void ReleaseLandingTiles(
        EntityUid mapUid,
        IReadOnlyList<Vector2i> newlyModifiedTiles,
        IReadOnlyList<(Vector2i Index, Tile Tile)> generatedTiles)
    {
        if (!TryComp<BiomeComponent>(mapUid, out var biome) ||
            !TryComp<MapGridComponent>(mapUid, out var mapGrid))
        {
            return;
        }

        var tilesToClear = new List<(Vector2i Index, Tile Tile)>(generatedTiles.Count);
        var changedGeneratedTiles = new HashSet<Vector2i>();
        foreach (var (index, tile) in generatedTiles)
        {
            if (_mapSystem.TryGetTileRef(mapUid, mapGrid, index, out var tileRef) && tileRef.Tile != tile)
            {
                changedGeneratedTiles.Add(index);
                continue;
            }

            tilesToClear.Add((index, Tile.Empty));
        }

        if (tilesToClear.Count > 0)
            _mapSystem.SetTiles(mapUid, mapGrid, tilesToClear);

        foreach (var index in newlyModifiedTiles)
        {
            if (changedGeneratedTiles.Contains(index))
                continue;

            var chunkOrigin = SharedMapSystem.GetChunkIndices(index, ChunkSize) * ChunkSize;
            if (!biome.ModifiedTiles.TryGetValue(chunkOrigin, out var modified) || !modified.Remove(index))
                continue;

            if (modified.Count == 0)
            {
                biome.ModifiedTiles.Remove(chunkOrigin);
                _tilePool.Return(modified);
            }
        }
    }
}
