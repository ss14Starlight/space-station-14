using System.Numerics;
using Content.Shared.Maps;
using Content.Shared._Starlight.Shadekin;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Threading;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Shadekin;

public sealed class LightGridSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    private readonly Dictionary<EntityUid, GridLightingState> _gridStates = new();
    private readonly Dictionary<EntityUid, List<WorldLightSourceData>> _mapLights = new();
    private readonly Dictionary<EntityUid, List<WorldLightSourceData>> _containerLights = new();
    private readonly Dictionary<EntityUid, TrackedGridState> _trackedLights = new();
    private readonly Dictionary<EntityUid, TrackedGridState> _trackedOccluders = new();
    private readonly Dictionary<EntityUid, TrackedGridState> _trackedShadegens = new();
    private readonly HashSet<EntityUid> _seenGrids = new();
    private readonly HashSet<EntityUid> _seenLights = new();
    private readonly HashSet<EntityUid> _seenOccluders = new();
    private readonly HashSet<EntityUid> _seenShadegens = new();
    private readonly HashSet<EntityUid> _dirtyGrids = new();
    private readonly List<EntityUid> _staleGrids = new();
    private readonly List<EntityUid> _staleTrackedEntities = new();
    // Reused every tick so we dont murder GC
    private readonly HashSet<Entity<OccluderComponent>> _occluders = new();
    private EntityQuery<OccluderComponent> _occluderQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(0.5f);

    private readonly HashSet<Vector2i> _opaque = new();
    private readonly HashSet<Vector2i> _scanTiles = new();
    private readonly HashSet<Vector2i> _blockedTiles = new();
    private readonly List<Entity<MapGridComponent>> _intersectingGrids = new();

    // cap for normalizing light from multiple overlapping sources
    private const float MaxExposure = 3f;
    private const float NearbyGridSearchRange = 3f;
    private static readonly Angle _directionalLightHalfAngle = Angle.FromDegrees(60f);
    private const float LightByteScale = 16f;
    private const float OpenSpaceExposure = MaxExposure;

    private LightJob _job;

    private readonly record struct TrackedGridState(EntityUid? GridUid, int StateHash);

    private sealed class GridLightingState
    {
        public MapGridComponent Grid = default!;
        public BroadphaseComponent Broadphase = default!;
        public readonly Dictionary<Vector2i, byte> LightMap = new();
        public readonly List<LightSourceData> LightSources = new();
        public readonly List<(Vector2i Tile, float Range)> ShadegenZones = new();
    }

    public override void Initialize()
    {
        base.Initialize();
        _occluderQuery = GetEntityQuery<OccluderComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();

        _job = new LightJob();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + _updateInterval;

        _mapLights.Clear();
        _containerLights.Clear();

        _dirtyGrids.Clear();
        _seenGrids.Clear();
        _seenLights.Clear();
        _seenOccluders.Clear();
        _seenShadegens.Clear();

        var gridQuery = EntityQueryEnumerator<MapGridComponent, BroadphaseComponent>();
        while (gridQuery.MoveNext(out var gridUid, out var gridComp, out var broadphase))
        {
            _seenGrids.Add(gridUid);
            var gridState = GetOrCreateGridState(gridUid);
            gridState.Grid = gridComp;
            gridState.Broadphase = broadphase;
            gridState.LightSources.Clear();
            gridState.ShadegenZones.Clear();
        }

        // remove light data for grids that no longer exist
        if (_gridStates.Count > 0)
        {
            _staleGrids.Clear();

            foreach (var cachedGrid in _gridStates.Keys)
            {
                if (_seenGrids.Contains(cachedGrid))
                    continue;

                _staleGrids.Add(cachedGrid);
            }

            foreach (var staleGrid in _staleGrids)
            {
                _gridStates.Remove(staleGrid);
                _dirtyGrids.Remove(staleGrid);
            }
        }

        var lightQuery = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (lightQuery.MoveNext(out var lightUid, out var lightComp, out var xform))
        {
            if (HasComp<DarkLightComponent>(lightUid) || HasComp<ShadegenAffectedComponent>(lightUid))
                continue;

            // deal with disabled light with negative energy
            if (!lightComp.Enabled
                || lightComp.Radius < 1
                || lightComp.Energy <= 0)
                continue;

            _seenLights.Add(lightUid);

            var coords = xform.Coordinates;
            if (lightComp.Offset != Vector2.Zero)
                coords = coords.Offset(xform.LocalRotation.RotateVec(lightComp.Offset));

            var brightness = GetLightBrightness(lightComp.Color, lightComp.Energy);
            if (brightness <= 0f)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            if (lightComp.Offset != Vector2.Zero)
                worldPos += _transform.GetWorldRotation(xform, _xformQuery).RotateVec(lightComp.Offset);

            var directional = lightComp.MaskPath != null;
            var worldDirection = directional
                ? (lightComp.MaskAutoRotate ? _transform.GetWorldRotation(xform, _xformQuery) + lightComp.Rotation : lightComp.Rotation)
                : Angle.Zero;

            if (GetLightBlockingContainer(lightUid) is { } containerUid)
            {
                UpdateTrackedState(_trackedLights, lightUid, new TrackedGridState(null, 0));
                GetOrCreateBucket(_containerLights, containerUid).Add(new WorldLightSourceData(
                    worldPos,
                    lightComp.Radius,
                    brightness,
                    worldDirection,
                    directional));
                continue;
            }

            if (xform.GridUid is { } gridUid && _gridStates.TryGetValue(gridUid, out var gridData))
            {
                var localPos = Vector2.Transform(worldPos, _transform.GetInvWorldMatrix(gridUid, _xformQuery));
                var tile = _maps.LocalToTile(gridUid, gridData.Grid, new EntityCoordinates(gridUid, localPos));
                var localDirection = directional
                    ? worldDirection - _transform.GetWorldRotation(gridUid, _xformQuery)
                    : Angle.Zero;
                var stateHash = HashCode.Combine(tile, lightComp.Radius, brightness, localDirection, lightComp.CastShadows, directional);
                UpdateTrackedState(_trackedLights, lightUid, new TrackedGridState(gridUid, stateHash));
                gridData.LightSources.Add(new LightSourceData(
                    tile,
                    lightComp.Radius,
                    brightness,
                    localDirection,
                    lightComp.CastShadows,
                    directional));
                continue;
            }

            UpdateTrackedState(_trackedLights, lightUid, new TrackedGridState(null, 0));

            if (xform.MapUid is not { } mapUid)
                continue;

            GetOrCreateBucket(_mapLights, mapUid).Add(new WorldLightSourceData(
                worldPos,
                lightComp.Radius,
                brightness,
                worldDirection,
                directional));
        }

        var occluderQuery = EntityQueryEnumerator<OccluderComponent, TransformComponent>();
        while (occluderQuery.MoveNext(out var occluderUid, out var occluder, out var occluderXform))
        {
            _seenOccluders.Add(occluderUid);

            if (occluderXform.GridUid is not { } gridUid)
            {
                UpdateTrackedState(_trackedOccluders, occluderUid, new TrackedGridState(null, 0));
                continue;
            }

            if (!_gridStates.TryGetValue(gridUid, out var gridData))
            {
                UpdateTrackedState(_trackedOccluders, occluderUid, new TrackedGridState(null, 0));
                continue;
            }

            var tile = _maps.LocalToTile(gridUid, gridData.Grid, occluderXform.Coordinates);
            var stateHash = HashCode.Combine(tile, occluder.Enabled);
            UpdateTrackedState(_trackedOccluders, occluderUid, new TrackedGridState(gridUid, stateHash));
        }

        var shadegenQuery = EntityQueryEnumerator<ShadegenComponent, TransformComponent>();
        while (shadegenQuery.MoveNext(out var shadegenUid, out var shadegen, out var shadegenXform))
        {
            _seenShadegens.Add(shadegenUid);

            if (shadegenXform.GridUid is not { } gridUid)
            {
                UpdateTrackedState(_trackedShadegens, shadegenUid, new TrackedGridState(null, 0));
                continue;
            }

            if (!_gridStates.TryGetValue(gridUid, out var gridData))
            {
                UpdateTrackedState(_trackedShadegens, shadegenUid, new TrackedGridState(null, 0));
                continue;
            }

            var tile = _maps.LocalToTile(gridUid, gridData.Grid, shadegenXform.Coordinates);
            gridData.ShadegenZones.Add((tile, shadegen.Range));
            var stateHash = HashCode.Combine(tile, shadegen.Range);
            UpdateTrackedState(_trackedShadegens, shadegenUid, new TrackedGridState(gridUid, stateHash));
        }

        CleanupTrackedStates(_trackedLights, _seenLights);
        CleanupTrackedStates(_trackedOccluders, _seenOccluders);
        CleanupTrackedStates(_trackedShadegens, _seenShadegens);

        if (_dirtyGrids.Count == 0)
            return;

        foreach (var gridUid in _dirtyGrids)
        {
            if (!_gridStates.TryGetValue(gridUid, out var gridData))
                continue;

            RebuildGrid(gridUid, gridData);
        }
    }

    private void UpdateTrackedState(Dictionary<EntityUid, TrackedGridState> trackedStates, EntityUid uid, TrackedGridState currentState)
    {
        if (!trackedStates.TryGetValue(uid, out var previousState))
        {
            trackedStates[uid] = currentState;

            if (currentState.GridUid is { } gridUid)
                _dirtyGrids.Add(gridUid);

            return;
        }

        if (previousState == currentState)
            return;

        if (previousState.GridUid is { } previousGrid)
            _dirtyGrids.Add(previousGrid);

        if (currentState.GridUid is { } currentGrid)
            _dirtyGrids.Add(currentGrid);

        trackedStates[uid] = currentState;
    }

    private void CleanupTrackedStates(Dictionary<EntityUid, TrackedGridState> trackedStates, HashSet<EntityUid> seenEntities)
    {
        _staleTrackedEntities.Clear();

        foreach (var trackedUid in trackedStates.Keys)
        {
            if (seenEntities.Contains(trackedUid))
                continue;

            _staleTrackedEntities.Add(trackedUid);
        }

        foreach (var trackedUid in _staleTrackedEntities)
        {
            if (trackedStates[trackedUid].GridUid is { } gridUid)
                _dirtyGrids.Add(gridUid);

            trackedStates.Remove(trackedUid);
        }
    }

    private static List<T> GetOrCreateBucket<T>(Dictionary<EntityUid, List<T>> buckets, EntityUid uid)
    {
        if (buckets.TryGetValue(uid, out var bucket))
            return bucket;

        bucket = new List<T>();
        buckets[uid] = bucket;
        return bucket;
    }

    private GridLightingState GetOrCreateGridState(EntityUid gridUid)
    {
        if (_gridStates.TryGetValue(gridUid, out var gridState))
            return gridState;

        gridState = new GridLightingState();
        _gridStates[gridUid] = gridState;
        return gridState;
    }

    private void RebuildGrid(EntityUid gridUid, GridLightingState gridState)
    {
        var lightMap = gridState.LightMap;
        var lightSources = gridState.LightSources;
        var shadegenZones = gridState.ShadegenZones;

        lightMap.Clear();
        _opaque.Clear();
        _scanTiles.Clear();
        _blockedTiles.Clear();

        foreach (var (center, range) in shadegenZones)
        {
            var rangeInt = (int)Math.Ceiling(range);
            for (var x = -rangeInt; x <= rangeInt; x++)
            {
                for (var y = -rangeInt; y <= rangeInt; y++)
                {
                    var dist = new Vector2(x, y).Length();
                    if (dist > range)
                        continue;

                    _blockedTiles.Add(center + new Vector2i(x, y));
                }
            }
        }

        if (lightSources.Count == 0)
            return;

        // Only scan tiles near lights, rather than scanning the entire grid
        foreach (var source in lightSources)
        {
            var range = (int)Math.Ceiling(source.Radius);
            for (var x = -range; x <= range; x++)
            {
                for (var y = -range; y <= range; y++)
                {
                    var tile = source.Tile + new Vector2i(x, y);
                    _scanTiles.Add(tile);
                }
            }
        }

        var grid = (gridUid, gridState.Broadphase, gridState.Grid);
        PopulateOpaqueTiles(grid);

        // Pre allocate slots for new light sources
        for (var i = _job.Vis1.Count; i < lightSources.Count; i++)
        {
            _job.Vis1.Add(null!);
            _job.Vis2.Add(null!);
            _job.OpaqueLocal.Add(null!);
            _job.BlockedLocal.Add(null!);
            _job.BoundaryArr.Add(null!);
            _job.LocalResults.Add(new Dictionary<Vector2i, float>());
        }

        if (_job.Vis1.Count > lightSources.Count)
        {
            var removeCount = _job.Vis1.Count - lightSources.Count;
            _job.Vis1.RemoveRange(lightSources.Count, removeCount);
            _job.Vis2.RemoveRange(lightSources.Count, removeCount);
            _job.OpaqueLocal.RemoveRange(lightSources.Count, removeCount);
            _job.BlockedLocal.RemoveRange(lightSources.Count, removeCount);
            _job.BoundaryArr.RemoveRange(lightSources.Count, removeCount);
            _job.LocalResults.RemoveRange(lightSources.Count, removeCount);
        }

        _job.LightSources = lightSources;
        _job.Opaque = _opaque;
        _job.BlockedTiles = _blockedTiles;
        _parallel.ProcessNow(_job, lightSources.Count);

        for (var i = 0; i < lightSources.Count; i++)
        {
            foreach (var (tile, intensity) in _job.LocalResults[i])
            {
                lightMap.TryGetValue(tile, out var existing);
                lightMap[tile] = EncodeLight(DecodeLight(existing) + intensity);
            }
        }
    }

    private void PopulateOpaqueTiles(Entity<BroadphaseComponent, MapGridComponent> grid)
    {
        if (_scanTiles.Count == 0)
            return;

        var first = true;
        var minTile = default(Vector2i);
        var maxTile = default(Vector2i);

        foreach (var tile in _scanTiles)
        {
            if (first)
            {
                minTile = tile;
                maxTile = tile;
                first = false;
                continue;
            }

            minTile = new Vector2i(Math.Min(minTile.X, tile.X), Math.Min(minTile.Y, tile.Y));
            maxTile = new Vector2i(Math.Max(maxTile.X, tile.X), Math.Max(maxTile.Y, tile.Y));
        }

        var minBounds = _lookup.GetLocalBounds(minTile, grid.Comp2.TileSize);
        var maxBounds = _lookup.GetLocalBounds(maxTile, grid.Comp2.TileSize);
        var bounds = new Box2(minBounds.BottomLeft, maxBounds.TopRight);

        _occluders.Clear();
        _lookup.GetLocalEntitiesIntersecting((grid.Owner, grid.Comp1), bounds, _occluders, query: _occluderQuery, flags: LookupFlags.Static | LookupFlags.Approximate);

        foreach (var occluder in _occluders)
        {
            if (!occluder.Comp.Enabled)
                continue;

            var xform = _xformQuery.GetComponent(occluder.Owner);
            var occTile = _maps.LocalToTile(grid.Owner, grid.Comp2, xform.Coordinates);
            if (!_scanTiles.Contains(occTile))
                continue;

            _opaque.Add(occTile);
        }
    }

    private static float GetLightBrightness(Color color, float energy)
    {
        var luminance = (0.2126f * color.R) + (0.7152f * color.G) + (0.0722f * color.B);
        return energy * luminance;
    }

    private static bool IsWithinDirectionalCone(Angle direction, bool directional, Vector2 delta)
    {
        if (!directional || delta == Vector2.Zero)
            return true;

        var angle = Angle.FromWorldVec(delta);
        var diff = Angle.ShortestDistance(direction, angle);
        return Math.Abs(diff.Theta) <= _directionalLightHalfAngle.Theta;
    }

    private static bool IsWithinDirectionalCone(LightSourceData source, Vector2i delta)
        => IsWithinDirectionalCone(source.Direction, source.Directional, new Vector2(delta.X, delta.Y));

    private static bool IsWithinDirectionalCone(WorldLightSourceData source, Vector2 delta)
        => IsWithinDirectionalCone(source.Direction, source.Directional, delta);

    private static float GetLightIntensity(float radius, float brightness, float dist)
    {
        if (dist > radius)
            return 0f;

        var ratio = dist / radius;
        var attenuation = 1f - (ratio * ratio);
        return brightness * attenuation * attenuation;
    }

    private static float GetLightIntensity(LightSourceData source, float dist)
        => GetLightIntensity(source.Radius, source.Brightness, dist);

    private static byte EncodeLight(float intensity)
    {
        if (intensity <= 0f) return 0;
        return (byte)Math.Min(intensity * (255f / LightByteScale) + 0.5f, 255f);
    }

    private static float DecodeLight(byte value)
    {
        return value * (LightByteScale / 255f);
    }

    public float GetExposure(EntityUid uid) => Math.Clamp(GetFullExposure(uid) / MaxExposure, 0f, 1f);

    public float GetFullExposure(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } mapUid)
            return GetLightBlockingContainer(uid) == null ? OpenSpaceExposure : 0f;

        var worldPos = _transform.GetWorldPosition(xform);

        if (GetLightBlockingContainer(uid) is { } containerUid)
            return GetDirectLightExposure(_containerLights.GetValueOrDefault(containerUid), worldPos);

        if (IsOpenSpace(xform))
            return OpenSpaceExposure;

        var size = NearbyGridSearchRange * 2f;
        var bounds = Box2.CenteredAround(worldPos, new Vector2(size, size));

        _intersectingGrids.Clear();
        var intersectingGrids = _intersectingGrids;
        _mapManager.FindGridsIntersecting(mapUid, bounds, ref intersectingGrids, includeMap: false);

        var exposure = 0f;

        foreach (var grid in _intersectingGrids)
        {
            if (!_gridStates.TryGetValue(grid.Owner, out var gridState))
                continue;

            var localPos = Vector2.Transform(worldPos, _transform.GetInvWorldMatrix(grid.Owner, _xformQuery));
            var tile = _maps.LocalToTile(grid.Owner, grid.Comp, new EntityCoordinates(grid.Owner, localPos));
            exposure += DecodeLight(gridState.LightMap.GetValueOrDefault(tile));
        }

        exposure += GetDirectLightExposure(_mapLights.GetValueOrDefault(mapUid), worldPos);
        exposure += GetMapAmbientExposure(xform.MapID);

        return exposure;
    }

    private bool IsOpenSpace(TransformComponent xform)
    {
        if (!_turf.TryGetTileRef(xform.Coordinates, out var tileRef))
            return true;

        return _turf.IsSpace(tileRef.Value);
    }

    private float GetDirectLightExposure(List<WorldLightSourceData>? lightSources, Vector2 worldPos)
    {
        if (lightSources == null)
            return 0f;

        var exposure = 0f;

        foreach (var source in lightSources)
        {
            var delta = worldPos - source.Position;
            if (!IsWithinDirectionalCone(source, delta))
                continue;

            var intensity = GetLightIntensity(source.Radius, source.Brightness, delta.Length());
            if (intensity > 0f)
                exposure += intensity;
        }

        return exposure;
    }

    private EntityUid? GetLightBlockingContainer(EntityUid uid)
    {
        var current = uid;

        while (Exists(current)
               && _container.TryGetContainingContainer(
                   (current, _xformQuery.GetComponent(current), _metaQuery.GetComponent(current)),
                   out var container))
        {
            if (container.OccludesLight)
                return container.Owner;

            current = container.Owner;
        }

        return null;
    }

    public byte GetTileLight(EntityUid gridUid, Vector2i tile)
    {
        if (!_gridStates.TryGetValue(gridUid, out var gridState))
            return 0;

        return gridState.LightMap.GetValueOrDefault(tile);
    }

    private float GetMapAmbientExposure(MapId mapId)
    {
        var mapEntity = _maps.GetMapOrInvalid(mapId);

        if (mapEntity == EntityUid.Invalid)
            return 0f;

        if (!TryComp<MapLightComponent>(mapEntity, out var mapLight))
            return 0f;

        return GetLightBrightness(mapLight.AmbientLightColor, 1f);
    }

    // The shadowcasting beast
    private record struct LightJob() : IParallelRobustJob
    {
        public int BatchSize => 16; // basically 16 lights per thread

        private const int VisEmpty = int.MinValue;

        public HashSet<Vector2i> Opaque = new();
        public HashSet<Vector2i> BlockedTiles = new();
        public List<LightSourceData> LightSources = new();
        public readonly List<int[]> Vis1 = new();
        public readonly List<int[]> Vis2 = new();
        public readonly List<bool[]> OpaqueLocal = new();
        public readonly List<bool[]> BlockedLocal = new();
        public readonly List<bool[]> BoundaryArr = new();
        public readonly List<Dictionary<Vector2i, float>> LocalResults = new();

        public void Execute(int index)
        {
            var source = LightSources[index];
            var results = LocalResults[index];
            results.Clear();

            var eyePos = source.Tile;
            var range = (int)Math.Ceiling(source.Radius);
            var side = 2 * range + 1;
            var gridSize = side * side;

            var opaqueLocal = EnsureBoolArray(OpaqueLocal, index, gridSize);
            var blockedLocal = EnsureBoolArray(BlockedLocal, index, gridSize);

            Array.Clear(opaqueLocal, 0, gridSize);
            Array.Clear(blockedLocal, 0, gridSize);

            for (var x = -range; x <= range; x++)
            {
                for (var y = -range; y <= range; y++)
                {
                    var idx = (x + range) * side + (y + range);
                    var tile = eyePos + new Vector2i(x, y);

                    if (Opaque.Contains(tile))
                        opaqueLocal[idx] = true;

                    if (BlockedTiles.Contains(tile))
                        blockedLocal[idx] = true;
                }
            }

            if (!source.CastShadows)
            {
                for (var x = -range; x <= range; x++)
                {
                    for (var y = -range; y <= range; y++)
                    {
                        if (!IsWithinDirectionalCone(source, new Vector2i(x, y)))
                            continue;

                        var idx = (x + range) * side + (y + range);
                        if (blockedLocal[idx])
                            continue;

                        var tile = eyePos + new Vector2i(x, y);

                        var dist = new Vector2(x, y).Length();
                        var intensity = GetLightIntensity(source, dist);

                        if (intensity > 0.01f)
                            results[tile] = intensity;
                    }
                }
                return;
            }

            var vis1 = EnsureArray(Vis1, index, gridSize);
            var vis2 = EnsureArray(Vis2, index, gridSize);
            var boundaryArr = EnsureBoolArray(BoundaryArr, index, gridSize);

            Array.Fill(vis1, VisEmpty, 0, gridSize);
            Array.Fill(vis2, VisEmpty, 0, gridSize);
            Array.Clear(boundaryArr, 0, gridSize);

            var ci = range * side + range;
            vis1[ci] = 0;
            vis2[ci] = 0;

            if (blockedLocal[ci])
                return;

            for (var depth = 1; depth <= range; depth++)
            {
                for (var x = -depth; x <= depth; x++)
                {
                    SetVisIfVisible(vis2, opaqueLocal, side, range, x, -depth, depth);
                    SetVisIfVisible(vis2, opaqueLocal, side, range, x, depth, depth);
                }

                for (var y = -depth + 1; y < depth; y++)
                {
                    SetVisIfVisible(vis2, opaqueLocal, side, range, -depth, y, depth);
                    SetVisIfVisible(vis2, opaqueLocal, side, range, depth, y, depth);
                }
            }

            for (var depth = 1; depth <= range * 2; depth++)
            {
                var minX = Math.Max(-range, -depth);
                var maxX = Math.Min(range, depth);

                for (var x = minX; x <= maxX; x++)
                {
                    var yAbs = depth - Math.Abs(x);
                    if (yAbs < 0 || yAbs > range)
                        continue;

                    SetVis1IfVisible(vis1, vis2, opaqueLocal, side, range, x, yAbs, depth);

                    if (yAbs != 0)
                        SetVis1IfVisible(vis1, vis2, opaqueLocal, side, range, x, -yAbs, depth);
                }
            }

            for (var x = -range; x <= range; x++)
            {
                for (var y = -range; y <= range; y++)
                {
                    var idx = (x + range) * side + (y + range);
                    if (!opaqueLocal[idx] || vis1[idx] != VisEmpty)
                        continue;

                    if (HasVisibleFace(opaqueLocal, vis1, side, range, x, y))
                        boundaryArr[idx] = true;
                }
            }

            for (var i = 0; i < gridSize; i++)
            {
                if (boundaryArr[i])
                    vis1[i] = -1;
            }

            // Collect results
            for (var x = -range; x <= range; x++)
            {
                for (var y = -range; y <= range; y++)
                {
                    if (vis1[(x + range) * side + (y + range)] == VisEmpty)
                        continue;

                    var delta = new Vector2i(x, y);
                    var dist = new Vector2(x, y).Length();

                    if (!IsWithinDirectionalCone(source, delta))
                        continue;

                    var idx = (x + range) * side + (y + range);
                    if (blockedLocal[idx])
                        continue;

                    var tile = eyePos + delta;

                    var intensity = GetLightIntensity(source, dist);

                    if (intensity > 0.01f)
                        results[tile] = intensity;
                }
            }

            // The light source tile itself
            if (!blockedLocal[ci] && !results.ContainsKey(eyePos))
                results[eyePos] = source.Brightness;
        }

        private static void SetVisIfVisible(int[] vis, bool[] opaque, int side, int range, int rx, int ry, int depth)
        {
            if (!CheckNeighborsVis(vis, opaque, side, range, rx, ry, depth - 1))
                return;

            var idx = (rx + range) * side + (ry + range);
            vis[idx] = opaque[idx] ? -1 : depth;
        }

        private static void SetVis1IfVisible(int[] vis1, int[] vis2, bool[] opaque, int side, int range, int rx, int ry, int depth)
        {
            if (!CheckNeighborsVis(vis1, opaque, side, range, rx, ry, depth - 1))
                return;

            var idx = (rx + range) * side + (ry + range);
            if (opaque[idx])
            {
                vis1[idx] = -1;
            }
            else
            {
                var v2 = vis2[idx];
                if (v2 != VisEmpty && v2 != 0)
                    vis1[idx] = depth;
            }
        }

        private static bool CheckNeighborsVis(int[] vis, bool[] opaque, int side, int range, int rx, int ry, int d)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var nx = rx + dx + range;
                    var ny = ry + dy + range;
                    if ((uint)nx >= (uint)side || (uint)ny >= (uint)side)
                        continue;

                    var val = vis[nx * side + ny];
                    if (val == VisEmpty || val != d)
                        continue;

                    if (dx != 0 && dy != 0)
                    {
                        var ai = (rx + dx + range) * side + (ry + range);
                        var bi = (rx + range) * side + (ry + dy + range);
                        if (opaque[ai] && opaque[bi])
                            continue;
                    }

                    return true;
                }
            }
            return false;
        }

        private static bool HasVisibleFace(bool[] opaque, int[] vis1, int side, int range, int rx, int ry)
        {
            var stepX = Math.Sign(rx);
            var stepY = Math.Sign(ry);

            if (stepX != 0)
            {
                var nx = rx - stepX + range;
                var ny = ry + range;
                if ((uint)nx < (uint)side && (uint)ny < (uint)side)
                {
                    var idx = nx * side + ny;
                    if (!opaque[idx] && vis1[idx] != VisEmpty)
                        return true;
                }
            }

            if (stepY != 0)
            {
                var nx = rx + range;
                var ny = ry - stepY + range;
                if ((uint)nx < (uint)side && (uint)ny < (uint)side)
                {
                    var idx = nx * side + ny;
                    if (!opaque[idx] && vis1[idx] != VisEmpty)
                        return true;
                }
            }

            return false;
        }

        private static int[] EnsureArray(List<int[]> list, int index, int minSize)
        {
            var arr = list[index];
            if (arr == null || arr.Length < minSize)
            {
                arr = new int[minSize];
                list[index] = arr;
            }
            return arr;
        }

        private static bool[] EnsureBoolArray(List<bool[]> list, int index, int minSize)
        {
            var arr = list[index];
            if (arr == null || arr.Length < minSize)
            {
                arr = new bool[minSize];
                list[index] = arr;
            }
            return arr;
        }
    }
}
