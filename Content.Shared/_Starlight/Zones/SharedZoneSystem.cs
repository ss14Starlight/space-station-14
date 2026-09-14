using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using DependencyAttribute = Robust.Shared.IoC.DependencyAttribute;

namespace Content.Shared._Starlight.Zones;

public abstract partial class SharedZoneSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedMapSystem Maps = default!;
    [Dependency] protected SharedTransformSystem XformSystem = default!;

    public const int ChunkSize = 8;
    public const int ChunkArea = ChunkSize * ChunkSize;

    private const int ChunkShift = 3;
    private const int ChunkMask = ChunkSize - 1;

    /// <summary>
    /// Represents a zone id that is not valid, meaning the tile is not in any zone.
    /// </summary>
    public const ushort NoZone = 0;

    /// <summary>
    /// Represents a region id that is not valid, meaning the tile is not in any region.
    /// </summary>
    public const ushort NoRegion = 0;

    private const int NoZonePriority = int.MinValue;

    private readonly Dictionary<string, ushort> _idByProto = [];

    private ZonePrototype?[] _protoById = [null];

    private int[] _priorityById = [NoZonePriority];

    private readonly Dictionary<string, ushort> _zoneByDoor = [];
    private readonly Dictionary<string, ushort> _doorCache = [];

    [Dependency] private EntityQuery<ZoneGridComponent> _zoneQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;

    private readonly List<(ZoneShapeSet Set, ushort Id, int Priority)> _rasterBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        BuildZoneIdTable();
        Proto.PrototypesReloaded += OnPrototypesReloaded;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Proto.PrototypesReloaded -= OnPrototypesReloaded;
    }

    #region Chunk maths

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i ChunkOrigin(Vector2i tile) => new(tile.X >> ChunkShift, tile.Y >> ChunkShift);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TileIndex(Vector2i tile) => ((tile.X & ChunkMask) << ChunkShift) | (tile.Y & ChunkMask);

    protected static ZoneChunk EnsureChunk(ZoneGridComponent comp, Vector2i tile)
    {
        var origin = ChunkOrigin(tile);

        if (!comp.Chunks.TryGetValue(origin, out var chunk))
            comp.Chunks[origin] = chunk = new ZoneChunk();

        return chunk;
    }

    protected static bool TryGetChunk(ZoneGridComponent comp, Vector2i tile, [NotNullWhen(true)] out ZoneChunk? chunk)
        => comp.Chunks.TryGetValue(ChunkOrigin(tile), out chunk);

    #endregion

    #region Lookup

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetZoneId(ZoneGridComponent comp, Vector2i tile)
    {
        var origin = ChunkOrigin(tile);
        var chunk = comp.CachedChunk;

        if (chunk == null || comp.CachedOrigin != origin)
        {
            if (!comp.Chunks.TryGetValue(origin, out chunk))
                return NoZone;

            comp.CachedChunk = chunk;
            comp.CachedOrigin = origin;
        }

        var index = TileIndex(tile);
        var region = chunk.Regions[index];

        if (region == NoRegion)
            return chunk.Hints[index];

        ref var entry = ref comp.Regions[region];

        return entry.Alias == NoRegion
            ? entry.Zone
            : comp.Regions[FindRoot(comp, region)].Zone;
    }

    /// <summary>
    /// Returns the zone id for a given tile on a grid, or <see cref="NoZone"/> if the tile is not in any zone.
    /// </summary>
    public ushort GetZoneId(EntityUid grid, Vector2i tile)
        => _zoneQuery.TryComp(grid, out var comp) ? GetZoneId(comp, tile) : NoZone;

    /// <summary>
    /// Returns the zone prototype for a given zone id, or null if the id is invalid.
    /// </summary>
    public ZonePrototype? GetZone(ushort id)
        => id < _protoById.Length ? _protoById[id] : null;

    /// <summary>
    /// Returns the zone id for a given zone prototype, or <see cref="NoZone"/> if the prototype is not registered.
    /// </summary>
    public ushort GetZoneId(ProtoId<ZonePrototype> zone)
        => _idByProto.GetValueOrDefault(zone.Id, NoZone);

    /// <summary>
    /// Returns the zone prototype for a given tile on a grid, or null if the tile is not in any zone.
    /// </summary>
    public bool TryGetZone(EntityUid grid, Vector2i tile, [NotNullWhen(true)] out ZonePrototype? zone)
    {
        zone = GetZone(GetZoneId(grid, tile));
        return zone != null;
    }

    /// <summary>
    /// Returns the zone prototype for a given world coordinate, or null if the coordinate is not in any zone.
    /// </summary>
    public bool TryGetZone(EntityCoordinates coords, [NotNullWhen(true)] out ZonePrototype? zone)
    {
        zone = null;

        var grid = XformSystem.GetGrid(coords);
        return grid != null && _gridQuery.TryComp(grid, out var gridComp) && TryGetZone(grid.Value, Maps.TileIndicesFor(grid.Value, gridComp, coords), out zone);
    }

    /// <summary>
    /// Returns the zone prototype for a given entity, or null if the entity is not in any zone.
    /// </summary>
    public bool TryGetZone(Entity<TransformComponent?> ent, [NotNullWhen(true)] out ZonePrototype? zone)
    {
        zone = null;

        return Resolve(ent.Owner, ref ent.Comp, false) &&
            ent.Comp.GridUid is { } grid &&
            _gridQuery.TryComp(grid, out var gridComp) &&
            TryGetZone(grid, Maps.TileIndicesFor(grid, gridComp, ent.Comp.Coordinates), out zone);
    }

    /// <summary>
    /// Returns true if the given tile on a grid is in the specified zone.
    /// </summary>
    public bool IsInZone(EntityUid grid, Vector2i tile, ProtoId<ZonePrototype> zone)
    {
        var id = GetZoneId(zone);
        return id != NoZone && GetZoneId(grid, tile) == id;
    }

    /// <summary>
    /// Returns the region id for a given tile on a grid, or <see cref="NoRegion"/> if the tile is not in any region.
    /// </summary>
    public static ushort GetRegion(ZoneGridComponent comp, Vector2i tile)
    {
        if (!TryGetChunk(comp, tile, out var chunk))
            return NoRegion;

        var region = chunk.Regions[TileIndex(tile)];
        return region == NoRegion ? NoRegion : FindRoot(comp, region);
    }

    /// <summary>
    /// Returns the region id for a given tile on a grid, or <see cref="NoRegion"/> if the tile is not in any region.
    /// </summary>
    public ushort GetRegion(EntityUid grid, Vector2i tile)
        => _zoneQuery.TryComp(grid, out var comp) ? GetRegion(comp, tile) : NoRegion;

    #endregion

    #region Regions

    public static ushort FindRoot(ZoneGridComponent comp, ushort region)
    {
        var root = region;
        while (comp.Regions[root].Alias != NoRegion)
            root = comp.Regions[root].Alias;

        while (comp.Regions[region].Alias != NoRegion)
        {
            var next = comp.Regions[region].Alias;
            comp.Regions[region].Alias = root;
            region = next;
        }

        return root;
    }

    protected static ushort AllocRegion(ZoneGridComponent comp)
    {
        ushort id;

        if (comp.FreeRegions.Count > 0)
        {
            id = comp.FreeRegions[^1];
            comp.FreeRegions.RemoveAt(comp.FreeRegions.Count - 1);
        }
        else
        {
            if (comp.RegionCount >= ushort.MaxValue)
                return NoRegion;

            if (comp.RegionCount == comp.Regions.Length)
                Array.Resize(ref comp.Regions, Math.Min(comp.Regions.Length * 2, ushort.MaxValue));

            id = (ushort) comp.RegionCount++;
        }

        comp.Regions[id] = new ZoneRegion { Used = true };
        return id;
    }

    protected static void TryFreeRegion(ZoneGridComponent comp, ushort region)
    {
        ref var entry = ref comp.Regions[region];

        if (!entry.Used || entry.Alias != NoRegion || entry.TileCount > 0 || entry.AliasCount > 0)
            return;

        entry = default;
        comp.FreeRegions.Add(region);
    }

    protected ushort MergeRegions(ZoneGridComponent comp, ushort a, ushort b)
    {
        a = FindRoot(comp, a);
        b = FindRoot(comp, b);

        if (a == b)
            return a;

        if (comp.Regions[a].TileCount < comp.Regions[b].TileCount)
            (a, b) = (b, a);

        ref var winner = ref comp.Regions[a];
        ref var loser = ref comp.Regions[b];

        if (winner.HintZone == loser.HintZone)
        {
            winner.HintTileCount += loser.HintTileCount;
        }
        else if (CompareZones(loser.HintZone, winner.HintZone) > 0)
        {
            winner.HintZone = loser.HintZone;
            winner.HintTileCount = loser.HintTileCount;
        }

        if (loser.MarkerZone != NoZone)
        {
            if (winner.MarkerZone == NoZone || loser.MarkerPriority > winner.MarkerPriority)
            {
                winner.MarkerZone = loser.MarkerZone;
                winner.MarkerPriority = loser.MarkerPriority;
                winner.MarkerConflict = loser.MarkerConflict;
            }
            else if (loser.MarkerPriority == winner.MarkerPriority)
            {
                winner.MarkerConflict |= loser.MarkerConflict || loser.MarkerZone != winner.MarkerZone;
            }
        }

        winner.TileCount += loser.TileCount;
        winner.AliasCount += loser.AliasCount + 1;

        ResolveZone(ref winner);

        loser.Alias = a;
        loser.TileCount = 0;
        loser.HintTileCount = 0;
        loser.AliasCount = 0;

        return a;
    }

    protected int CompareZones(ushort a, ushort b)
    {
        if (a == b)
            return 0;

        var cmp = ZonePriority(a).CompareTo(ZonePriority(b));
        return cmp != 0 ? cmp : a.CompareTo(b);
    }

    protected int ZonePriority(ushort zone)
        => zone < _priorityById.Length ? _priorityById[zone] : NoZonePriority;

    protected virtual void QueueFullRebuild(Entity<ZoneGridComponent> ent)
        => ent.Comp.NeedsFullRebuild = true;

    #endregion

    #region Rasterisation

    [SubscribeLocalEvent]
    private void OnZoneGridInit(Entity<ZoneGridComponent> ent, ref ComponentInit _)
        => RebuildHints(ent);

    public void RebuildHints(Entity<ZoneGridComponent> ent)
    {
        var comp = ent.Comp;

        foreach (var chunk in comp.Chunks.Values)
        {
            Array.Clear(chunk.Hints);
        }

        comp.InvalidateCache();

        if (comp.Shapes.Count == 0)
            return;

        _rasterBuffer.Clear();

        foreach (var set in comp.Shapes)
        {
            if (set.IsEmpty)
                continue;

            if (!Proto.TryIndex(set.Zone, out var proto))
            {
                Log.Error($"Unknown zone prototype {set.Zone.Id} on grid {ToPrettyString(ent.Owner)}.");
                continue;
            }

            _rasterBuffer.Add((set, _idByProto[proto.ID], proto.Priority));
        }

        _rasterBuffer.Sort(static (a, b) =>
        {
            var cmp = a.Priority.CompareTo(b.Priority);
            return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
        });

        foreach (var (set, id, _) in _rasterBuffer)
        {
            foreach (var rect in set.Rects)
            {
                for (var x = rect.Left; x < rect.Right; x++)
                for (var y = rect.Bottom; y < rect.Top; y++)
                    PaintHint(comp, new Vector2i(x, y), id);
            }

            foreach (var circle in set.Circles)
            {
                var bounds = circle.Bounds();

                for (var x = bounds.Left; x < bounds.Right; x++)
                for (var y = bounds.Bottom; y < bounds.Top; y++)
                {
                    if (circle.ContainsTile(x, y))
                        PaintHint(comp, new Vector2i(x, y), id);
                }
            }
        }

        _rasterBuffer.Clear();
    }

    private static void PaintHint(ZoneGridComponent comp, Vector2i tile, ushort id)
        => EnsureChunk(comp, tile).Hints[TileIndex(tile)] = id;

    protected static void ResolveZone(ref ZoneRegion region)
    {
        var marker = region.MarkerConflict ? NoZone : region.MarkerZone;

        region.Zone = marker != NoZone ? marker : region.HintZone;
    }

    #endregion

    #region Door prototypes

    public ushort GetDoorZone(string? prototype)
    {
        if (prototype == null)
            return NoZone;

        if (_doorCache.TryGetValue(prototype, out var cached))
            return cached;

        var resolved = ResolveDoorZone(prototype, 0);
        _doorCache[prototype] = resolved;
        return resolved;
    }

    private ushort ResolveDoorZone(string prototype, int depth)
    {
        if (depth > 16)
            return NoZone;

        if (_zoneByDoor.TryGetValue(prototype, out var direct))
            return direct;

        if (!Proto.TryIndex<EntityPrototype>(prototype, out var proto) || proto.Parents == null)
            return NoZone;

        foreach (var parent in proto.Parents)
        {
            var resolved = ResolveDoorZone(parent, depth + 1);

            if (resolved != NoZone)
                return resolved;
        }

        return NoZone;
    }

    #endregion

    #region Zone id table

    private void BuildZoneIdTable()
    {
        _idByProto.Clear();

        var protos = new List<ZonePrototype>(Proto.Count<ZonePrototype>());
        foreach (var proto in Proto.EnumeratePrototypes<ZonePrototype>())
        {
            protos.Add(proto);
        }

        protos.Sort(static (a, b) => string.CompareOrdinal(a.ID, b.ID));

        if (protos.Count >= ushort.MaxValue)
            throw new InvalidOperationException($"Too many {nameof(ZonePrototype)}s, zone ids are ushort.");

        _protoById = new ZonePrototype?[protos.Count + 1];
        _priorityById = new int[protos.Count + 1];
        _priorityById[NoZone] = NoZonePriority;

        _zoneByDoor.Clear();
        _doorCache.Clear();

        for (var i = 0; i < protos.Count; i++)
        {
            var id = (ushort) (i + 1);
            _protoById[id] = protos[i];
            _priorityById[id] = protos[i].Priority;
            _idByProto[protos[i].ID] = id;

            foreach (var door in protos[i].Doors)
            {
                if (_zoneByDoor.TryGetValue(door.Id, out var existing))
                {
                    Log.Error($"Door prototype {door.Id} is claimed by both {_protoById[existing]!.ID} and {protos[i].ID}.");
                    continue;
                }

                _zoneByDoor[door.Id] = id;
            }
        }
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<ZonePrototype>())
            return;

        BuildZoneIdTable();

        var query = AllEntityQuery<ZoneGridComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            RebuildHints((uid, comp));
            QueueFullRebuild((uid, comp));
        }
    }

    #endregion
}
