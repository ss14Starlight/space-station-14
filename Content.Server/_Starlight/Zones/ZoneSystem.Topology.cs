using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.Zones;
using Content.Shared.Atmos;
using Content.Shared.Pinpointer;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.Zones;

public sealed partial class ZoneSystem
{
    private static readonly (Vector2i Offset, int Blocked, int BlockedBack)[] _neighbours =
    [
        (new Vector2i(0, 1), BlockMask(AtmosDirection.North), BlockMask(AtmosDirection.South)),
        (new Vector2i(0, -1), BlockMask(AtmosDirection.South), BlockMask(AtmosDirection.North)),
        (new Vector2i(1, 0), BlockMask(AtmosDirection.East), BlockMask(AtmosDirection.West)),
        (new Vector2i(-1, 0), BlockMask(AtmosDirection.West), BlockMask(AtmosDirection.East)),
    ];

    [Dependency] private EntityQuery<ZoneGridComponent> _zoneQuery = default!;
    [Dependency] private EntityQuery<NavMapComponent> _navQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery = default!;
    [Dependency] private EntityQuery<AirtightComponent> _airtightQuery = default!;
    [Dependency] private EntityQuery<NavMapDoorComponent> _doorQuery = default!;
    [Dependency] private EntityQuery<ZoneBoundaryExemptComponent> _exemptQuery = default!;

    private readonly HashSet<EntityUid> _dirtyGrids = [];
    private readonly List<EntityUid> _gridBuffer = [];

    private List<Vector2i> _seedBuffer = default!;
    private ushort[] _anchorRegions = default!;
    private Vector2i[] _anchors = default!;

    private readonly Queue<Vector2i> _floodQueue = new();
    private readonly List<Vector2i> _floodTiles = [];
    private readonly HashSet<Vector2i> _floodSet = [];

    private readonly Queue<Vector2i> _raceQueueA = new();
    private readonly Queue<Vector2i> _raceQueueB = new();
    private readonly List<Vector2i> _raceTilesA = [];
    private readonly List<Vector2i> _raceTilesB = [];
    private readonly HashSet<Vector2i> _raceSetA = [];
    private readonly HashSet<Vector2i> _raceSetB = [];

    private static int BlockMask(AtmosDirection dir)
        => ((int) dir << (int) NavMapChunkType.Wall) | ((int) dir << (int) NavMapChunkType.Airlock);

    private readonly record struct ZoneContext(EntityUid Grid, MapGridComponent GridComp, NavMapComponent Nav);

    #region Events

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<ZoneGridComponent> ent, ref MapInitEvent args)
        => QueueFullRebuild(ent);

    [SubscribeLocalEvent]
    private void OnAirtightChanged(ref AirtightChanged args)
    {
        if (args.AirBlockedChanged)
            return;

        DirtyTile(args.Position.Grid, args.Position.Tile);

        var current = args.Airtight.LastPosition;
        if (current != args.Position)
            DirtyTile(current.Grid, current.Tile);
    }

    [SubscribeLocalEvent]
    private void OnTileChanged(ref TileChangedEvent ev)
    {
        if (!_zoneQuery.HasComp(ev.Entity))
            return;

        foreach (var change in ev.Changes)
        {
            if (change.EmptyChanged)
                DirtyTile(ev.Entity, change.GridIndices);
        }
    }

    [SubscribeLocalEvent]
    private void OnGridSplit(ref GridSplitEvent args)
    {
        if (!_zoneQuery.TryComp(args.Grid, out var oldComp))
            return;

        if (!_mapGridQuery.TryComp(args.Grid, out var oldGrid))
            return;

        foreach (var newGridUid in args.NewGrids)
        {
            if (newGridUid == args.Grid || !_mapGridQuery.TryComp(newGridUid, out var newGrid))
                continue;

            var newComp = EnsureComp<ZoneGridComponent>(newGridUid);

            if (TryGetSplitOffset((newGridUid, newGrid), (args.Grid, oldGrid), out var offset))
                newComp.Shapes = TranslateShapes(oldComp.Shapes, -offset);

            RebuildHints((newGridUid, newComp));
            QueueFullRebuild((newGridUid, newComp));
        }

        QueueFullRebuild((args.Grid, oldComp));
    }

    private bool TryGetSplitOffset(
        Entity<MapGridComponent> newGrid,
        Entity<MapGridComponent> oldGrid,
        out Vector2i offset)
    {
        offset = default;

        var enumerator = Maps.GetAllTiles(newGrid, newGrid);
        if (!enumerator.MoveNext(out var tile))
            return false;

        var newTile = tile.Value.GridIndices;
        var world = Maps.GridTileToWorldPos(newGrid, newGrid, newTile);
        var oldTile = Maps.WorldToTile(oldGrid, oldGrid, world);

        offset = newTile - oldTile;
        return true;
    }

    private static List<ZoneShapeSet> TranslateShapes(List<ZoneShapeSet> shapes, Vector2i offset)
    {
        var result = new List<ZoneShapeSet>(shapes.Count);

        foreach (var set in shapes)
        {
            var copy = new ZoneShapeSet { Zone = set.Zone };

            foreach (var rect in set.Rects)
            {
                copy.Rects.Add(rect.Translated(offset));
            }

            foreach (var circle in set.Circles)
            {
                copy.Circles.Add(new ZoneCircle
                {
                    Center = circle.Center + offset,
                    Radius = circle.Radius,
                });
            }

            result.Add(copy);
        }

        return result;
    }

    private void DirtyTile(EntityUid grid, Vector2i tile)
    {
        if (!_zoneQuery.TryComp(grid, out var comp))
            return;

        if (comp.DirtySet.Add(tile))
            comp.DirtyTiles.Enqueue(tile);

        _dirtyGrids.Add(grid);
    }

    protected override void QueueFullRebuild(Entity<ZoneGridComponent> ent)
    {
        base.QueueFullRebuild(ent);
        _dirtyGrids.Add(ent.Owner);
    }

    private static void QueueRename(ZoneGridComponent comp, ushort region, Vector2i seed)
        => comp.RenameQueue.Enqueue((region, seed));

    #endregion

    #region Update

    public override void Update(float frameTime)
    {
        ProcessDirtyGrids();
        UpdateTracking();
    }

    private void ProcessDirtyGrids()
    {
        if (_dirtyGrids.Count == 0)
            return;

        _gridBuffer.Clear();
        _gridBuffer.AddRange(_dirtyGrids);

        foreach (var gridUid in _gridBuffer)
        {
            if (!_zoneQuery.TryComp(gridUid, out var comp) ||
                !_navQuery.TryComp(gridUid, out var nav) ||
                !_mapGridQuery.TryComp(gridUid, out var grid))
            {
                _dirtyGrids.Remove(gridUid);
                continue;
            }

            var ent = (gridUid, comp);
            var ctx = new ZoneContext(gridUid, grid, nav);
            var changed = false;

            if (!comp.NeedsFullRebuild)
            {
                var budget = _maxTilesPerTick;

                while (budget-- > 0 && !comp.NeedsFullRebuild && comp.DirtyTiles.TryDequeue(out var tile))
                {
                    comp.DirtySet.Remove(tile);
                    ProcessTile(ent, ctx, tile);
                    changed = true;
                }

                var renames = _maxRenamesPerTick;

                while (renames-- > 0 && !comp.NeedsFullRebuild && comp.RenameQueue.TryDequeue(out var rename))
                {
                    RederiveRegion(ent, ctx, rename.Region, rename.Seed);
                    changed = true;
                }
            }

            if (comp.NeedsFullRebuild)
            {
                FullRebuild(ent, nav);
                changed = true;
            }

            if (changed)
                comp.Revision++;

            if (comp.DirtyTiles.Count == 0 && comp.RenameQueue.Count == 0)
                _dirtyGrids.Remove(gridUid);
        }
    }

    #endregion

    #region Full rebuild

    /// <summary>
    /// Rebuilds the entire zone topology for a grid. This is a heavy operation and should be used sparingly.
    /// </summary>
    public void FullRebuild(Entity<ZoneGridComponent> ent, NavMapComponent? nav = null)
    {
        var comp = ent.Comp;

        comp.NeedsFullRebuild = false;
        comp.DirtyTiles.Clear();
        comp.DirtySet.Clear();
        comp.RenameQueue.Clear();

        foreach (var chunk in comp.Chunks.Values)
        {
            Array.Clear(chunk.Regions);
            Array.Clear(chunk.Markers);
            Array.Clear(chunk.MarkerPriorities);
        }

        comp.Regions = new ZoneRegion[16];
        comp.RegionCount = 1;
        comp.FreeRegions.Clear();
        comp.InvalidateCache();

        if (!Resolve(ent.Owner, ref nav, false) || !_mapGridQuery.TryComp(ent.Owner, out var grid))
            return;

        var ctx = new ZoneContext(ent.Owner, grid, nav);

        foreach (var (origin, navChunk) in nav.Chunks)
        {
            for (var index = 0; index < navChunk.TileData.Length; index++)
            {
                if ((navChunk.TileData[index] & SharedNavMapSystem.AirlockMask) == 0)
                    continue;

                RefreshMarkerArea(ctx, comp, TileOf(origin, index));
            }
        }

        var markers = AllEntityQuery<ZoneMarkerComponent, TransformComponent>();

        while (markers.MoveNext(out _, out _, out var markerXform))
        {
            if (markerXform.GridUid != ent.Owner)
                continue;

            RefreshMarkerArea(ctx, comp, Maps.TileIndicesFor(ent.Owner, grid, markerXform.Coordinates));
        }

        foreach (var (origin, navChunk) in nav.Chunks)
        {
            for (var index = 0; index < navChunk.TileData.Length; index++)
            {
                var tile = TileOf(origin, index);

                if (!IsRegionTile(ctx, tile))
                    continue;

                if (RawRegion(comp, tile) != NoRegion)
                    continue;

                FloodNewRegion(ent, ctx, tile);
            }
        }

        comp.RenameQueue.Clear();
    }

    private static Vector2i TileOf(Vector2i chunkOrigin, int index)
        => (chunkOrigin * SharedNavMapSystem.ChunkSize) + SharedNavMapSystem.GetTileFromIndex(index);

    #endregion

    #region Region building

    private bool FloodFrom(ZoneContext ctx, Vector2i seed, int budget)
    {
        _floodQueue.Clear();
        _floodTiles.Clear();
        _floodSet.Clear();

        _floodQueue.Enqueue(seed);
        _floodSet.Add(seed);

        while (_floodQueue.TryDequeue(out var current))
        {
            if (_floodTiles.Count >= budget)
                return false;

            _floodTiles.Add(current);

            if (!TryGetFlag(ctx, current, out var flag))
                continue;

            for (var i = 0; i < _neighbours.Length; i++)
            {
                var neighbour = current + _neighbours[i].Offset;

                if (_floodSet.Contains(neighbour))
                    continue;

                if (!CanConnect(ctx, flag, neighbour, i))
                    continue;

                _floodSet.Add(neighbour);
                _floodQueue.Enqueue(neighbour);
            }
        }

        return true;
    }

    private ushort FloodNewRegion(Entity<ZoneGridComponent> ent, ZoneContext ctx, Vector2i seed)
    {
        var comp = ent.Comp;

        FloodFrom(ctx, seed, int.MaxValue);

        var region = AllocRegion(comp);

        if (region == NoRegion)
        {
            Log.Error($"Ran out of zone region ids on {ToPrettyString(ent.Owner)}.");
            comp.NeedsFullRebuild = true;
            return NoRegion;
        }

        var naming = new ZoneNaming();

        foreach (var tile in _floodTiles)
        {
            var chunk = EnsureChunk(comp, tile);
            var index = TileIndex(tile);

            chunk.Regions[index] = region;
            Accumulate(ref naming, chunk, index);
        }

        var entry = new ZoneRegion
        {
            Used = true,
            HintZone = naming.Hint,
            HintTileCount = naming.HintTiles,
            MarkerZone = naming.Marker,
            MarkerPriority = naming.MarkerPriority,
            MarkerConflict = naming.MarkerConflict,
            TileCount = _floodTiles.Count,
        };

        ResolveZone(ref entry);
        comp.Regions[region] = entry;

        return region;
    }

    private void RederiveRegion(Entity<ZoneGridComponent> ent, ZoneContext ctx, ushort region, Vector2i seed)
    {
        var comp = ent.Comp;

        if (GetRegion(comp, seed) != region)
            return;

        if (!FloodFrom(ctx, seed, _maxSearchVisits))
        {
            comp.NeedsFullRebuild = true;
            return;
        }

        var naming = new ZoneNaming();

        foreach (var tile in _floodTiles)
        {
            if (!TryGetChunk(comp, tile, out var chunk))
                continue;

            Accumulate(ref naming, chunk, TileIndex(tile));
        }

        ref var entry = ref comp.Regions[region];
        entry.HintZone = naming.Hint;
        entry.HintTileCount = naming.HintTiles;
        entry.MarkerZone = naming.Marker;
        entry.MarkerPriority = naming.MarkerPriority;
        entry.MarkerConflict = naming.MarkerConflict;
        entry.TileCount = _floodTiles.Count;

        ResolveZone(ref entry);
    }

    private struct ZoneNaming
    {
        public ushort Hint;
        public int HintTiles;
        public ushort Marker;
        public short MarkerPriority;
        public bool MarkerConflict;
    }

    private void Accumulate(ref ZoneNaming naming, ZoneChunk chunk, int index)
    {
        var hint = chunk.Hints[index];

        if (hint == naming.Hint)
            naming.HintTiles++;
        else if (CompareZones(hint, naming.Hint) > 0)
        {
            naming.Hint = hint;
            naming.HintTiles = 1;
        }

        var marker = chunk.Markers[index];

        if (marker == NoZone)
            return;

        var priority = chunk.MarkerPriorities[index];

        if (naming.Marker == NoZone || priority > naming.MarkerPriority)
        {
            naming.Marker = marker;
            naming.MarkerPriority = priority;
            naming.MarkerConflict = false;
        }
        else if (priority == naming.MarkerPriority && marker != naming.Marker)
            naming.MarkerConflict = true;
    }

    #endregion

    #region Incremental update

    private void ProcessTile(Entity<ZoneGridComponent> ent, ZoneContext ctx, Vector2i tile)
    {
        var comp = ent.Comp;

        foreach (var offset in _markerSpread)
            RefreshMarker(ctx, comp, tile + offset);

        var passable = TryGetFlag(ctx, tile, out var flag) && IsRegionTile(flag);

        if (!passable)
        {
            DetachTile(ent, tile);

            _seedBuffer.Clear();

            for (var i = 0; i < _neighbours.Length; i++)
            {
                var neighbour = tile + _neighbours[i].Offset;

                if (GetRegion(comp, neighbour) != NoRegion)
                    _seedBuffer.Add(neighbour);
            }

            CheckSplits(ent, ctx);
            return;
        }

        var chunk = EnsureChunk(comp, tile);
        var index = TileIndex(tile);
        var region = chunk.Regions[index] == NoRegion ? NoRegion : FindRoot(comp, chunk.Regions[index]);

        for (var i = 0; i < _neighbours.Length; i++)
        {
            var neighbour = tile + _neighbours[i].Offset;

            if (!CanConnect(ctx, flag, neighbour, i))
                continue;

            var other = GetRegion(comp, neighbour);

            if (other == NoRegion)
                continue;

            region = region == NoRegion ? other : MergeRegions(comp, region, other);
        }

        if (region == NoRegion)
        {
            FloodNewRegion(ent, ctx, tile);
            return;
        }

        if (chunk.Regions[index] == NoRegion)
            AttachTile(comp, chunk, index, region);
        else
            chunk.Regions[index] = region;

        _seedBuffer.Clear();
        _seedBuffer.Add(tile);

        for (var i = 0; i < _neighbours.Length; i++)
        {
            var neighbour = tile + _neighbours[i].Offset;

            if (CanConnect(ctx, flag, neighbour, i))
                continue;

            if (GetRegion(comp, neighbour) == region)
                _seedBuffer.Add(neighbour);
        }

        if (_seedBuffer.Count > 1)
            CheckSplits(ent, ctx);
    }

    private void AttachTile(ZoneGridComponent comp, ZoneChunk chunk, int index, ushort region)
    {
        chunk.Regions[index] = region;

        ref var entry = ref comp.Regions[region];
        entry.TileCount++;

        var naming = new ZoneNaming
        {
            Hint = entry.HintZone,
            HintTiles = entry.HintTileCount,
            Marker = entry.MarkerZone,
            MarkerPriority = entry.MarkerPriority,
            MarkerConflict = entry.MarkerConflict,
        };

        Accumulate(ref naming, chunk, index);

        entry.HintZone = naming.Hint;
        entry.HintTileCount = naming.HintTiles;
        entry.MarkerZone = naming.Marker;
        entry.MarkerPriority = naming.MarkerPriority;
        entry.MarkerConflict = naming.MarkerConflict;

        ResolveZone(ref entry);
    }

    private void DetachTile(Entity<ZoneGridComponent> ent, Vector2i tile)
    {
        var comp = ent.Comp;

        if (!TryGetChunk(comp, tile, out var chunk))
            return;

        var index = TileIndex(tile);
        var region = chunk.Regions[index];

        if (region == NoRegion)
            return;

        var root = FindRoot(comp, region);
        chunk.Regions[index] = NoRegion;

        ref var entry = ref comp.Regions[root];
        entry.TileCount--;

        if (chunk.Hints[index] == entry.HintZone)
            entry.HintTileCount--;

        var lostMarker = chunk.Markers[index] != NoZone;

        if (entry.TileCount <= 0)
        {
            TryFreeRegion(comp, root);
            return;
        }

        if (!lostMarker && (entry.HintZone == NoZone || entry.HintTileCount > 0))
            return;

        for (var i = 0; i < _neighbours.Length; i++)
        {
            var neighbour = tile + _neighbours[i].Offset;

            if (GetRegion(comp, neighbour) != root)
                continue;

            QueueRename(comp, root, neighbour);
            return;
        }

        comp.NeedsFullRebuild = true;
    }

    #endregion

    #region Split detection

    private void CheckSplits(Entity<ZoneGridComponent> ent, ZoneContext ctx)
    {
        if (_seedBuffer.Count < 2)
            return;

        var comp = ent.Comp;
        var anchorCount = 0;

        foreach (var seed in _seedBuffer)
        {
            var region = GetRegion(comp, seed);

            if (region == NoRegion)
                continue;

            var anchor = -1;

            for (var i = 0; i < anchorCount; i++)
            {
                if (_anchorRegions[i] != region)
                    continue;

                anchor = i;
                break;
            }

            if (anchor < 0)
            {
                if (anchorCount >= _maxSeeds)
                {
                    comp.NeedsFullRebuild = true;
                    return;
                }

                _anchorRegions[anchorCount] = region;
                _anchors[anchorCount] = seed;
                anchorCount++;
                continue;
            }

            switch (Race(ctx, _anchors[anchor], seed, out var closed))
            {
                case RaceResult.Connected:
                    continue;

                case RaceResult.Overflow:
                    comp.NeedsFullRebuild = true;
                    return;

                case RaceResult.Split:
                    var anchorClosed = closed == _raceTilesA;

                    Separate(ent, closed);

                    if (anchorClosed)
                        _anchors[anchor] = seed;

                    _anchorRegions[anchor] = GetRegion(comp, _anchors[anchor]);
                    continue;
            }
        }
    }

    private enum RaceResult : byte
    {
        Connected,
        Split,
        Overflow,
    }

    private RaceResult Race(ZoneContext ctx, Vector2i a, Vector2i b, out List<Vector2i> closed)
    {
        _raceQueueA.Clear();
        _raceQueueB.Clear();
        _raceTilesA.Clear();
        _raceTilesB.Clear();
        _raceSetA.Clear();
        _raceSetB.Clear();

        closed = _raceTilesA;

        if (a == b)
            return RaceResult.Connected;

        _raceQueueA.Enqueue(a);
        _raceSetA.Add(a);
        _raceTilesA.Add(a);

        _raceQueueB.Enqueue(b);
        _raceSetB.Add(b);
        _raceTilesB.Add(b);

        var visits = 0;

        while (true)
        {
            if (_raceQueueA.Count == 0)
            {
                closed = _raceTilesA;
                return RaceResult.Split;
            }

            if (_raceQueueB.Count == 0)
            {
                closed = _raceTilesB;
                return RaceResult.Split;
            }

            if (++visits > _maxSearchVisits)
                return RaceResult.Overflow;

            var stepA = _raceTilesA.Count <= _raceTilesB.Count;

            var queue = stepA ? _raceQueueA : _raceQueueB;
            var mine = stepA ? _raceSetA : _raceSetB;
            var theirs = stepA ? _raceSetB : _raceSetA;
            var tiles = stepA ? _raceTilesA : _raceTilesB;

            var current = queue.Dequeue();

            if (!TryGetFlag(ctx, current, out var flag))
                continue;

            for (var i = 0; i < _neighbours.Length; i++)
            {
                var neighbour = current + _neighbours[i].Offset;

                if (!CanConnect(ctx, flag, neighbour, i))
                    continue;

                if (theirs.Contains(neighbour))
                    return RaceResult.Connected;

                if (!mine.Add(neighbour))
                    continue;

                tiles.Add(neighbour);
                queue.Enqueue(neighbour);
            }
        }
    }

    private void Separate(Entity<ZoneGridComponent> ent, List<Vector2i> tiles)
    {
        var comp = ent.Comp;

        if (tiles.Count == 0)
            return;

        var old = GetRegion(comp, tiles[0]);
        var region = AllocRegion(comp);

        if (region == NoRegion)
        {
            comp.NeedsFullRebuild = true;
            return;
        }

        var naming = new ZoneNaming();
        var lostHintTiles = 0;
        var lostMarker = false;
        var oldHint = old == NoRegion ? NoZone : comp.Regions[old].HintZone;

        foreach (var tile in tiles)
        {
            var chunk = EnsureChunk(comp, tile);
            var index = TileIndex(tile);

            chunk.Regions[index] = region;

            if (chunk.Hints[index] == oldHint)
                lostHintTiles++;

            if (chunk.Markers[index] != NoZone)
                lostMarker = true;

            Accumulate(ref naming, chunk, index);
        }

        var fresh = new ZoneRegion
        {
            Used = true,
            HintZone = naming.Hint,
            HintTileCount = naming.HintTiles,
            MarkerZone = naming.Marker,
            MarkerPriority = naming.MarkerPriority,
            MarkerConflict = naming.MarkerConflict,
            TileCount = tiles.Count,
        };

        ResolveZone(ref fresh);
        comp.Regions[region] = fresh;

        if (old == NoRegion)
            return;

        ref var entry = ref comp.Regions[old];
        entry.TileCount -= tiles.Count;
        entry.HintTileCount -= lostHintTiles;

        if (entry.TileCount <= 0)
            TryFreeRegion(comp, old);
        else if (lostMarker || (entry.HintZone != NoZone && entry.HintTileCount <= 0))
            QueueRename(comp, old, FindTileIn(comp, old, tiles));
    }

    private Vector2i FindTileIn(ZoneGridComponent comp, ushort region, List<Vector2i> near)
    {
        foreach (var tile in near)
        {
            for (var i = 0; i < _neighbours.Length; i++)
            {
                var neighbour = tile + _neighbours[i].Offset;

                if (GetRegion(comp, neighbour) == region)
                    return neighbour;
            }
        }

        comp.NeedsFullRebuild = true;
        return default;
    }

    #endregion

    #region Tile queries

    private bool TryGetFlag(ZoneContext ctx, Vector2i tile, out int flag)
    {
        var origin = SharedMapSystem.GetChunkIndices(tile, SharedNavMapSystem.ChunkSize);

        if (!ctx.Nav.Chunks.TryGetValue(origin, out var chunk))
        {
            flag = 0;
            return false;
        }

        var relative = SharedMapSystem.GetChunkRelative(tile, SharedNavMapSystem.ChunkSize);
        flag = chunk.TileData[SharedNavMapSystem.GetTileIndex(relative)];

        if ((flag & SharedNavMapSystem.AirlockMask) != 0)
            flag = ExcludeExemptDoors(ctx, tile, flag);

        return true;
    }

    private int ExcludeExemptDoors(ZoneContext ctx, Vector2i tile, int flag)
    {
        var doors = 0;
        var exempt = false;

        var enumerator = Maps.GetAnchoredEntities(ctx.Grid, ctx.GridComp, tile);

        while (enumerator.MoveNext(out var uid))
        {
            if (!_doorQuery.HasComp(uid) || !_airtightQuery.TryComp(uid, out var airtight))
                continue;

            if (_exemptQuery.HasComp(uid))
            {
                exempt = true;
                continue;
            }

            doors |= (int) airtight.AirBlockedDirection;
        }

        return !exempt ? flag : (flag & ~SharedNavMapSystem.AirlockMask) | (doors << (int) NavMapChunkType.Airlock);
    }

    private static bool IsRegionTile(int flag)
        => (flag & SharedNavMapSystem.FloorMask) != 0 &&
        (flag & SharedNavMapSystem.WallMask) != SharedNavMapSystem.WallMask &&
        (flag & SharedNavMapSystem.AirlockMask) != SharedNavMapSystem.AirlockMask;

    private bool IsRegionTile(ZoneContext ctx, Vector2i tile)
        => TryGetFlag(ctx, tile, out var flag) && IsRegionTile(flag);

    private bool CanConnect(ZoneContext ctx, int flag, Vector2i neighbour, int direction)
        => (flag & _neighbours[direction].Blocked) == 0 &&
        TryGetFlag(ctx, neighbour, out var neighbourFlag) &&
        IsRegionTile(neighbourFlag) &&
        (neighbourFlag & _neighbours[direction].BlockedBack) == 0;

    private static ushort RawRegion(ZoneGridComponent comp, Vector2i tile)
        => TryGetChunk(comp, tile, out var chunk) ? chunk.Regions[TileIndex(tile)] : NoRegion;

    #endregion
}
