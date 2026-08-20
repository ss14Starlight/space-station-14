using Content.Client.Decals.Overlays;
using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using static Content.Shared.Decals.DecalGridComponent;

namespace Content.Client.Decals
{
    public sealed partial class DecalSystem : SharedDecalSystem
    {
        [Dependency] private IOverlayManager _overlayManager = default!;
        [Dependency] private SpriteSystem _sprites = default!;

        private DecalOverlay? _overlay;

        private HashSet<uint> _removedUids = new();
        private readonly List<Vector2i> _removedChunks = new();

        public override void Initialize()
        {
            base.Initialize();

            _overlay = new DecalOverlay(_sprites, EntityManager, PrototypeManager);
            _overlayManager.AddOverlay(_overlay);

            SubscribeLocalEvent<DecalGridComponent, ComponentHandleState>(OnHandleState);
            SubscribeNetworkEvent<DecalChunkUpdateEvent>(OnChunkUpdate);
        }

        public void ToggleOverlay()
        {
            if (_overlay == null)
                return;

            if (_overlayManager.HasOverlay<DecalOverlay>())
            {
                _overlayManager.RemoveOverlay(_overlay);
            }
            else
            {
                _overlayManager.AddOverlay(_overlay);
            }
        }

        public override void Shutdown()
        {
            base.Shutdown();

            if (_overlay == null)
                return;

            _overlayManager.RemoveOverlay(_overlay);
        }

        protected override void OnDecalRemoved(EntityUid gridId, uint decalId, DecalGridComponent component, Vector2i indices, DecalChunk chunk)
        {
            base.OnDecalRemoved(gridId, decalId, component, indices, chunk);
            DebugTools.Assert(chunk.Decals.ContainsKey(decalId));
            chunk.Decals.Remove(decalId);
        }

        private void OnHandleState(EntityUid gridUid, DecalGridComponent gridComp, ref ComponentHandleState args)
        {
            // is this a delta or full state?
            _removedChunks.Clear();
            Dictionary<Vector2i, DecalChunk> modifiedChunks;

            switch (args.Current)
            {
                case DecalGridDeltaState delta:
                {
                    modifiedChunks = delta.ModifiedChunks;
                    foreach (var key in gridComp.ChunkCollection.ChunkCollection.Keys)
                    {
                        if (!delta.AllChunks.Contains(key))
                            _removedChunks.Add(key);
                    }

                    break;
                }
                case DecalGridState state:
                {
                    modifiedChunks = state.Chunks;
                    foreach (var key in gridComp.ChunkCollection.ChunkCollection.Keys)
                    {
                        if (!state.Chunks.ContainsKey(key))
                            _removedChunks.Add(key);
                    }

                    break;
                }
                default:
                    return;
            }

            if (_removedChunks.Count > 0)
                RemoveChunks(gridUid, gridComp, _removedChunks);

            if (modifiedChunks.Count > 0)
                UpdateChunks(gridUid, gridComp, modifiedChunks);
        }

        private void OnChunkUpdate(DecalChunkUpdateEvent ev)
        {
            foreach (var (netGrid, updatedGridChunks) in ev.Data)
            {
                if (updatedGridChunks.Count == 0)
                    continue;

                var gridId = GetEntity(netGrid);

                if (!TryComp(gridId, out DecalGridComponent? gridComp))
                {
                    Log.Error($"Received decal information for an entity without a decal component: {ToPrettyString(gridId)}");
                    continue;
                }

                UpdateChunks(gridId, gridComp, updatedGridChunks);
            }

            // Starlight Start: Diff chunks
            foreach (var (netGrid, diffs) in ev.Diffs)
            {
                if (diffs.Count == 0)
                    continue;

                var gridId = GetEntity(netGrid);

                if (!TryComp(gridId, out DecalGridComponent? gridComp))
                {
                    Log.Error($"Received decal diff for an entity without a decal component: {ToPrettyString(gridId)}");
                    continue;
                }

                ApplyChunkDiffs(gridId, gridComp, diffs);
            }
            // Starlight End

            // Now we'll cull old chunks out of range as the server will send them to us anyway.
            foreach (var (netGrid, chunks) in ev.RemovedChunks)
            {
                if (chunks.Count == 0)
                    continue;

                var gridId = GetEntity(netGrid);

                if (!TryComp(gridId, out DecalGridComponent? gridComp))
                {
                    Log.Error($"Received decal information for an entity without a decal component: {ToPrettyString(gridId)}");
                    continue;
                }

                RemoveChunks(gridId, gridComp, chunks);
            }
        }

        // Starlight Start: Merge decal diffs instead of wholesale replace
        private void ApplyChunkDiffs(EntityUid gridId, DecalGridComponent gridComp, Dictionary<Vector2i, DecalChunkDiff> diffs)
        {
            var chunkCollection = gridComp.ChunkCollection.ChunkCollection;

            foreach (var (index, diff) in diffs)
            {
                if (!chunkCollection.TryGetValue(index, out var chunk))
                {
                    // We should always have this chunk already but if something desynced, don't crash, just start
                    // a fresh chunk from the diff and let the next full resync correct anything missed.
                    chunk = new DecalChunk();
                    chunkCollection[index] = chunk;
                }

                foreach (var removedId in diff.Removed)
                {
                    if (chunk.Decals.ContainsKey(removedId))
                    {
                        OnDecalRemoved(gridId, removedId, gridComp, index, chunk);
                        gridComp.DecalIndex.Remove(removedId);
                    }
                }

                foreach (var (decalId, decal) in diff.Upserted)
                {
                    chunk.Decals[decalId] = decal;
                    gridComp.DecalIndex[decalId] = index;
                }
            }
        }
        // Starlight End

        private void UpdateChunks(EntityUid gridId, DecalGridComponent gridComp, Dictionary<Vector2i, DecalChunk> updatedGridChunks)
        {
            var chunkCollection = gridComp.ChunkCollection.ChunkCollection;

            // Update any existing data / remove decals we didn't receive data for.
            foreach (var (indices, newChunkData) in updatedGridChunks)
            {
                if (chunkCollection.TryGetValue(indices, out var chunk))
                {
                    _removedUids.Clear();
                    _removedUids.UnionWith(chunk.Decals.Keys);
                    _removedUids.ExceptWith(newChunkData.Decals.Keys);
                    foreach (var removedUid in _removedUids)
                    {
                        OnDecalRemoved(gridId, removedUid, gridComp, indices, chunk);
                        gridComp.DecalIndex.Remove(removedUid);
                    }
                }

                chunkCollection[indices] = newChunkData;

                foreach (var (uid, decal) in newChunkData.Decals)
                {
                    gridComp.DecalIndex[uid] = indices;
                }
            }
        }

        private void RemoveChunks(EntityUid gridId, DecalGridComponent gridComp, IEnumerable<Vector2i> chunks)
        {
            var chunkCollection = gridComp.ChunkCollection.ChunkCollection;

            foreach (var index in chunks)
            {
                if (!chunkCollection.TryGetValue(index, out var chunk))
                    continue;

                foreach (var decalId  in chunk.Decals.Keys)
                {
                    OnDecalRemoved(gridId, decalId, gridComp, index, chunk);
                    gridComp.DecalIndex.Remove(decalId);
                }

                chunkCollection.Remove(index);
            }
        }
    }
}
