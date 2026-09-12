namespace Content.Shared._Starlight.Zones;

[RegisterComponent]
[Access(typeof(SharedZoneSystem))]
public sealed partial class ZoneGridComponent : Component
{
    /// <summary>
    /// List of zone shapes which will be used to mark zones on this grid.
    /// </summary>
    [DataField(serverOnly: true)]
    [Access(Other = AccessPermissions.ReadWriteExecute)]
    public List<ZoneShapeSet> Shapes = [];

    [ViewVariables]
    public readonly Dictionary<Vector2i, ZoneChunk> Chunks = [];

    [ViewVariables]
    public ZoneRegion[] Regions = new ZoneRegion[16];

    [ViewVariables]
    public int RegionCount = 1;

    [ViewVariables]
    public readonly List<ushort> FreeRegions = [];

    [ViewVariables]
    public readonly Queue<Vector2i> DirtyTiles = new();

    [ViewVariables]
    public readonly HashSet<Vector2i> DirtySet = [];

    [ViewVariables]
    public readonly Queue<(ushort Region, Vector2i Seed)> RenameQueue = new();

    [ViewVariables]
    public bool NeedsFullRebuild;

    [ViewVariables]
    public int Revision;

    [ViewVariables]
    public ZoneChunk? CachedChunk;

    [ViewVariables]
    public Vector2i CachedOrigin;

    /// <summary>
    /// Invalidates cache of last accessed chunk, so next access will recalculate it.
    /// </summary>
    public void InvalidateCache()
    {
        CachedChunk = null;
        CachedOrigin = default;
    }
}

public sealed class ZoneChunk
{
    public readonly ushort[] Hints = new ushort[SharedZoneSystem.ChunkArea];

    public readonly ushort[] Markers = new ushort[SharedZoneSystem.ChunkArea];

    public readonly short[] MarkerPriorities = new short[SharedZoneSystem.ChunkArea];

    public readonly ushort[] Regions = new ushort[SharedZoneSystem.ChunkArea];
}

public struct ZoneRegion
{
    public ushort Alias;

    public ushort Zone;

    public ushort HintZone;

    public int HintTileCount;

    public ushort MarkerZone;

    public short MarkerPriority;

    public bool MarkerConflict;

    public int TileCount;

    public int AliasCount;

    public bool Used;
}
