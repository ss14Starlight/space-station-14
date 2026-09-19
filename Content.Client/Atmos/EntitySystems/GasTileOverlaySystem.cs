using System.Numerics;
using Content.Client.Atmos.Overlays;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameStates;

namespace Content.Client.Atmos.EntitySystems;

[UsedImplicitly]
public sealed partial class GasTileOverlaySystem : SharedGasTileOverlaySystem
{
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private SpriteSystem _spriteSys = default!;
    [Dependency] private SharedTransformSystem _xformSys = default!;

    private GasTileOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GasOverlayUpdateEvent>(HandleGasOverlayUpdate);
        SubscribeLocalEvent<GasTileOverlayComponent, ComponentHandleState>(OnHandleState);

        _overlay = new GasTileOverlay(this, EntityManager, _resourceCache, ProtoMan, _spriteSys, _xformSys);
        _overlayMan.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<GasTileOverlay>();
    }

    private void OnHandleState(EntityUid gridUid, GasTileOverlayComponent comp, ref ComponentHandleState args)
    {
        Dictionary<Vector2i, GasOverlayChunk> modifiedChunks;

        switch (args.Current)
        {
            // is this a delta or full state?
            case GasTileOverlayDeltaState delta:
            {
                modifiedChunks = delta.ModifiedChunks;
                foreach (var index in comp.Chunks.Keys)
                {
                    if (!delta.AllChunks.Contains(index))
                        comp.Chunks.Remove(index);
                }

                break;
            }
            case GasTileOverlayState state:
            {
                modifiedChunks = state.Chunks;
                foreach (var index in comp.Chunks.Keys)
                {
                    if (!state.Chunks.ContainsKey(index))
                        comp.Chunks.Remove(index);
                }

                break;
            }
            default:
                return;
        }

        foreach (var (index, data) in modifiedChunks)
        {
            comp.Chunks[index] = data;
        }
    }

    private void HandleGasOverlayUpdate(GasOverlayUpdateEvent ev)
    {
        foreach (var (nent, removedIndicies) in ev.RemovedChunks)
        {
            var grid = GetEntity(nent);

            if (!TryComp(grid, out GasTileOverlayComponent? comp))
                continue;

            foreach (var index in removedIndicies)
            {
                comp.Chunks.Remove(index);
            }
        }

        foreach (var (nent, gridData) in ev.UpdatedChunks)
        {
            var grid = GetEntity(nent);

            if (!TryComp(grid, out GasTileOverlayComponent? comp))
                continue;

            foreach (var chunkData in gridData)
            {
                comp.Chunks[chunkData.Index] = chunkData;
            }
        }

        // Starlight-start: chunks we already have only get the tiles that changed.
        foreach (var (nent, deltas) in ev.DeltaChunks)
        {
            var grid = GetEntity(nent);

            if (!TryComp(grid, out GasTileOverlayComponent? comp))
                continue;

            foreach (var delta in deltas)
            {
                if (comp.Chunks.TryGetValue(delta.Index, out var chunk))
                    ApplyChunkDelta(chunk, delta);
            }
        }
        // Starlight-end
    }


    #region Starlight
    private static void ApplyChunkDelta(GasOverlayChunk chunk, GasOverlayChunkDelta delta)
    {
        var tiles = delta.Tiles;
        var i = 0;

        while (tiles != 0 && i < delta.Data.Length)
        {
            var dataIndex = BitOperations.TrailingZeroCount(tiles);
            tiles &= tiles - 1;

            if (dataIndex >= chunk.TileData.Length)
                break;

            chunk.TileData[dataIndex] = delta.Data[i++];
        }
    }
    #endregion
}
