using Robust.Shared.Serialization;
using static Content.Shared.Decals.DecalGridComponent;

namespace Content.Shared.Decals
{
    [Serializable, NetSerializable]
    public sealed class DecalChunkUpdateEvent : EntityEventArgs
    {
        public Dictionary<NetEntity, Dictionary<Vector2i, DecalChunk>> Data = new();
        public Dictionary<NetEntity, HashSet<Vector2i>> RemovedChunks = new();

        public Dictionary<NetEntity, Dictionary<Vector2i, DecalChunkDiff>> Diffs = []; // Starlight
    }
    // Starlight Start
    [Serializable, NetSerializable]
    public sealed class DecalChunkDiff
    {
        public Dictionary<uint, Decal> Upserted = [];
        public HashSet<uint> Removed = [];
    }
    // Starlight End
}

