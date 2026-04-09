using System.Numerics;
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
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, float>> _lightGrids = new();
    private readonly Dictionary<EntityUid, List<WorldLightSourceData>> _mapLights = new();
    private readonly Dictionary<EntityUid, List<WorldLightSourceData>> _containerLights = new();
    // Reused every tick so we dont murder GC
    private readonly HashSet<Entity<OccluderComponent>> _occluders = new();
    private EntityQuery<OccluderComponent> _occluderQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(0.35f);

    private readonly HashSet<Vector2i> _opaque = new();
    private readonly HashSet<Vector2i> _scanTiles = new();
    private readonly List<Entity<MapGridComponent>> _intersectingGrids = new();

    // cap for normalizing light from multiple overlapping sources
    private const float MaxExposure = 3f;
    private const float NearbyGridSearchRange = 3f;
    private const int NeighborSampleRadius = 2;
    private static readonly Angle _directionalLightHalfAngle = Angle.FromDegrees(60f);

    private LightJob _job;

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

        var grids = new Dictionary<EntityUid, (MapGridComponent Grid, BroadphaseComponent Broadphase)>();
        var lightSourcesByGrid = new Dictionary<EntityUid, List<LightSourceData>>();
        var shadegenZonesByGrid = new Dictionary<EntityUid, List<(Vector2i Tile, float Range)>>();

        var gridQuery = EntityQueryEnumerator<MapGridComponent, BroadphaseComponent>();
        while (gridQuery.MoveNext(out var gridUid, out var gridComp, out var broadphase))
        {
            grids[gridUid] = (gridComp, broadphase);
        }

        // remove light data for grids that no longer exist
        if (_lightGrids.Count > 0)
        {
            List<EntityUid>? staleGrids = null;

            foreach (var cachedGrid in _lightGrids.Keys)
            {
                if (grids.ContainsKey(cachedGrid))
                    continue;

                staleGrids ??= new List<EntityUid>();
                staleGrids.Add(cachedGrid);
            }

            if (staleGrids != null)
            {
                foreach (var staleGrid in staleGrids)
                {
                    _lightGrids.Remove(staleGrid);
                }
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
            var localDirection = directional
                ? (lightComp.MaskAutoRotate ? xform.LocalRotation + lightComp.Rotation : lightComp.Rotation)
                : Angle.Zero;
            var worldDirection = directional
                ? (lightComp.MaskAutoRotate ? _transform.GetWorldRotation(xform, _xformQuery) + lightComp.Rotation : lightComp.Rotation)
                : Angle.Zero;

            if (GetLightBlockingContainer(lightUid) is { } containerUid)
            {
                GetOrCreateBucket(_containerLights, containerUid).Add(new WorldLightSourceData(
                    worldPos,
                    lightComp.Radius,
                    brightness,
                    worldDirection,
                    directional));
                continue;
            }

            if (xform.GridUid is { } gridUid && grids.TryGetValue(gridUid, out var gridData))
            {
                var tile = _maps.LocalToTile(gridUid, gridData.Grid, coords);
                var sources = GetOrCreateBucket(lightSourcesByGrid, gridUid);

                sources.Add(new LightSourceData(
                    tile,
                    lightComp.Radius,
                    brightness,
                    localDirection,
                    lightComp.CastShadows,
                    directional));
                continue;
            }

            if (xform.MapUid is not { } mapUid)
                continue;

            GetOrCreateBucket(_mapLights, mapUid).Add(new WorldLightSourceData(
                worldPos,
                lightComp.Radius,
                brightness,
                worldDirection,
                directional));
        }

        var shadegenQuery = EntityQueryEnumerator<ShadegenComponent, TransformComponent>();
        while (shadegenQuery.MoveNext(out _, out var shadegen, out var shadegenXform))
        {
            if (shadegenXform.GridUid is not { } gridUid)
                continue;

            if (!grids.TryGetValue(gridUid, out var gridData))
                continue;

            var tile = _maps.LocalToTile(gridUid, gridData.Grid, shadegenXform.Coordinates);
            var zones = GetOrCreateBucket(shadegenZonesByGrid, gridUid);
            zones.Add((tile, shadegen.Range));
        }

        foreach (var (gridUid, gridData) in grids)
        {
            lightSourcesByGrid.TryGetValue(gridUid, out var lightSources);
            shadegenZonesByGrid.TryGetValue(gridUid, out var shadegenZones);
            RebuildGrid(gridUid, gridData.Grid, gridData.Broadphase, lightSources, shadegenZones);
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

    private void RebuildGrid(
        EntityUid gridUid,
        MapGridComponent gridComp,
        BroadphaseComponent broadphase,
        List<LightSourceData>? lightSources,
        List<(Vector2i Tile, float Range)>? shadegenZones)
    {
        if (!_lightGrids.TryGetValue(gridUid, out var lightMap))
        {
            lightMap = new Dictionary<Vector2i, float>();
            _lightGrids[gridUid] = lightMap;
        }

        lightMap.Clear();
        _opaque.Clear();
        _scanTiles.Clear();

        if (lightSources is null || lightSources.Count == 0)
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

        var grid = (gridUid, broadphase, gridComp);
        PopulateOpaqueTiles(grid);

        // Pre allocate slots for new light sources
        for (var i = _job.Vis1.Count; i < lightSources.Count; i++)
        {
            _job.Vis1.Add(null!);
            _job.Vis2.Add(null!);
            _job.OpaqueLocal.Add(null!);
            _job.BoundaryArr.Add(null!);
            _job.LocalResults.Add(new Dictionary<Vector2i, float>());
        }

        _job.LightSources = lightSources;
        _job.Opaque = _opaque;
        _parallel.ProcessNow(_job, lightSources.Count);

        for (var i = 0; i < lightSources.Count; i++)
        {
            foreach (var (tile, intensity) in _job.LocalResults[i])
            {
                lightMap.TryGetValue(tile, out var existing);
                lightMap[tile] = existing + intensity;
            }
        }

        if (shadegenZones is null)
            return;

        // wipe everything in shadegen radius
        foreach (var (center, range) in shadegenZones)
        {
            var rangeInt = (int)Math.Ceiling(range);
            for (var x = -rangeInt; x <= rangeInt; x++)
            {
                for (var y = -rangeInt; y <= rangeInt; y++)
                {
                    var tile = center + new Vector2i(x, y);
                    var dist = new Vector2(x, y).Length();
                    if (dist <= range)
                        lightMap.Remove(tile);
                }
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

    public float GetExposure(EntityUid uid) => Math.Clamp(GetFullExposure(uid) / MaxExposure, 0f, 1f);

    public float GetFullExposure(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } mapUid)
            return 0f;

        var worldPos = _transform.GetWorldPosition(xform);

        if (GetLightBlockingContainer(uid) is { } containerUid)
            return GetDirectLightExposure(_containerLights.GetValueOrDefault(containerUid), worldPos);

        var size = NearbyGridSearchRange * 2f;
        var bounds = Box2.CenteredAround(worldPos, new Vector2(size, size));

        _intersectingGrids.Clear();
        var intersectingGrids = _intersectingGrids;
        _mapManager.FindGridsIntersecting(mapUid, bounds, ref intersectingGrids, includeMap: false);

        var exposure = 0f;

        foreach (var grid in _intersectingGrids)
        {
            if (!_lightGrids.TryGetValue(grid.Owner, out var lightMap))
                continue;

            var localPos = Vector2.Transform(worldPos, _transform.GetInvWorldMatrix(grid.Owner, _xformQuery));
            var tile = _maps.LocalToTile(grid.Owner, grid.Comp, new EntityCoordinates(grid.Owner, localPos));

            // entity directly on lit tile
            if (lightMap.TryGetValue(tile, out var direct))
            {
                exposure += direct;
                continue;
            }

            // entity is offgrid or on unlit tile
            var best = 0f;
            for (var dx = -NeighborSampleRadius; dx <= NeighborSampleRadius; dx++)
            {
                for (var dy = -NeighborSampleRadius; dy <= NeighborSampleRadius; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var neighbor = tile + new Vector2i(dx, dy);
                    if (!lightMap.TryGetValue(neighbor, out var nVal) || nVal <= 0f)
                        continue;

                    var dist = new Vector2(dx, dy).Length();
                    var attenuation = 1f / (1f + dist * dist);
                    var effective = nVal * attenuation;

                    if (effective > best)
                        best = effective;
                }
            }

            exposure += best;
        }

        exposure += GetDirectLightExposure(_mapLights.GetValueOrDefault(mapUid), worldPos);

        return exposure;
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

    public float GetTileLight(EntityUid gridUid, Vector2i tile)
    {
        if (!_lightGrids.TryGetValue(gridUid, out var lightMap))
            return 0f;

        return lightMap.GetValueOrDefault(tile, 0f);
    }

    // The shadowcasting beast
    private record struct LightJob() : IParallelRobustJob
    {
        public int BatchSize => 16; // basically 16 lights per thread

        private const int VisEmpty = int.MinValue;

        public HashSet<Vector2i> Opaque = new();
        public List<LightSourceData> LightSources = new();
        public readonly List<int[]> Vis1 = new();
        public readonly List<int[]> Vis2 = new();
        public readonly List<bool[]> OpaqueLocal = new();
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

            if (!source.CastShadows)
            {
                for (var x = -range; x <= range; x++)
                {
                    for (var y = -range; y <= range; y++)
                    {
                        if (!IsWithinDirectionalCone(source, new Vector2i(x, y)))
                            continue;

                        var dist = new Vector2(x, y).Length();
                        var intensity = GetLightIntensity(source, dist);

                        if (intensity > 0.01f)
                            results[eyePos + new Vector2i(x, y)] = intensity;
                    }
                }
                return;
            }

            var vis1 = EnsureArray(Vis1, index, gridSize);
            var vis2 = EnsureArray(Vis2, index, gridSize);
            var opaqueLocal = EnsureBoolArray(OpaqueLocal, index, gridSize);
            var boundaryArr = EnsureBoolArray(BoundaryArr, index, gridSize);

            Array.Fill(vis1, VisEmpty, 0, gridSize);
            Array.Fill(vis2, VisEmpty, 0, gridSize);
            Array.Clear(opaqueLocal, 0, gridSize);
            Array.Clear(boundaryArr, 0, gridSize);

            // Build local opaque grid from shared set
            for (var x = -range; x <= range; x++)
            {
                for (var y = -range; y <= range; y++)
                {
                    if (Opaque.Contains(eyePos + new Vector2i(x, y)))
                        opaqueLocal[(x + range) * side + (y + range)] = true;
                }
            }

            var ci = range * side + range;
            vis1[ci] = 0;
            vis2[ci] = 0;

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

                    var intensity = GetLightIntensity(source, dist);

                    if (intensity > 0.01f)
                        results[eyePos + delta] = intensity;
                }
            }

            // The light source tile itself
            if (!results.ContainsKey(eyePos))
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
