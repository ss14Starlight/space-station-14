using System.Numerics;
using Content.Shared._Starlight.Shadekin;
using Robust.Server.GameObjects;
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
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, float>> _lightGrids = new();
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

    private record struct LightSourceData(
        Vector2i Tile,
        float Radius,
        float Brightness,
        Angle Direction,
        bool CastShadows,
        bool Directional);

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

        if (grids.Count == 0)
            return;

        var lightQuery = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (lightQuery.MoveNext(out var lightUid, out var lightComp, out var xform))
        {
            if (xform.GridUid is not { } gridUid)
                continue;

            if (!grids.TryGetValue(gridUid, out var gridData))
                continue;

            if (HasComp<DarkLightComponent>(lightUid) || HasComp<ShadegenAffectedComponent>(lightUid))
                continue;

            if ((_metaQuery.GetComponent(lightUid).Flags & MetaDataFlags.InContainer) != 0)
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

            var tile = _maps.LocalToTile(gridUid, gridData.Grid, coords);
            var sources = GetOrCreateBucket(lightSourcesByGrid, gridUid);
            var directional = lightComp.MaskPath != null;
            var direction = directional
                ? (lightComp.MaskAutoRotate ? xform.LocalRotation + lightComp.Rotation : lightComp.Rotation)
                : Angle.Zero;

            sources.Add(new LightSourceData(
                tile,
                lightComp.Radius,
                brightness,
                direction,
                lightComp.CastShadows,
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
            _job.Vis1.Add(new Dictionary<Vector2i, int>());
            _job.Vis2.Add(new Dictionary<Vector2i, int>());
            _job.SeedTiles.Add(new HashSet<Vector2i>());
            _job.BoundaryTiles.Add(new HashSet<Vector2i>());
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

    private static bool IsWithinDirectionalCone(LightSourceData source, Vector2i delta)
    {
        if (!source.Directional || delta == Vector2i.Zero)
            return true;

        var angle = Angle.FromWorldVec(new Vector2(delta.X, delta.Y));
        var diff = Angle.ShortestDistance(source.Direction, angle);
        return Math.Abs(diff.Theta) <= _directionalLightHalfAngle.Theta;
    }

    private static float GetLightIntensity(LightSourceData source, float dist)
    {
        if (dist > source.Radius)
            return 0f;

        var ratio = dist / source.Radius;
        var attenuation = 1f - (ratio * ratio);
        return source.Brightness * attenuation * attenuation;
    }

    public float GetExposure(EntityUid uid) => Math.Clamp(GetFullExposure(uid) / MaxExposure, 0f, 1f);

    public float GetFullExposure(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } mapUid)
            return 0f;

        var worldPos = _transform.GetWorldPosition(xform);
        var size = NearbyGridSearchRange * 2f;
        var bounds = Box2.CenteredAround(worldPos, new Vector2(size, size));

        _intersectingGrids.Clear();
        var intersectingGrids = _intersectingGrids;
        _mapManager.FindGridsIntersecting(mapUid, bounds, ref intersectingGrids, includeMap: false);

        if (_intersectingGrids.Count == 0)
            return 0f;

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

        return exposure;
    }

    public float GetTileLight(EntityUid gridUid, Vector2i tile)
    {
        if (!_lightGrids.TryGetValue(gridUid, out var lightMap))
            return 0f;

        return lightMap.GetValueOrDefault(tile, 0f);
    }

    #region Shadowcasting helpers
    private static bool CheckNeighborsVis(Dictionary<Vector2i, int> vis, HashSet<Vector2i> opaque, Vector2i index, int d)
    {
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var neighbor = index + new Vector2i(x, y);
                if (!vis.TryGetValue(neighbor, out var depth) || depth != d)
                    continue;

                if (x != 0 && y != 0)
                {
                    var cardinalA = index + new Vector2i(x, 0);
                    var cardinalB = index + new Vector2i(0, y);
                    if (opaque.Contains(cardinalA) && opaque.Contains(cardinalB))
                        continue;
                }

                return true;
            }
        }
        return false;
    }
    private static bool HasVisibleFace(
        HashSet<Vector2i> tiles,
        HashSet<Vector2i> blocked,
        Dictionary<Vector2i, int> vis1,
        Vector2i index,
        Vector2i center)
    {
        var delta = index - center;
        var stepX = Math.Sign(delta.X);
        var stepY = Math.Sign(delta.Y);

        if (stepX != 0)
        {
            var neighbor = index - new Vector2i(stepX, 0);
            if (tiles.Contains(neighbor) &&
                !blocked.Contains(neighbor) &&
                vis1.ContainsKey(neighbor))
            {
                return true;
            }
        }

        if (stepY != 0)
        {
            var neighbor = index - new Vector2i(0, stepY);
            if (tiles.Contains(neighbor) &&
                !blocked.Contains(neighbor) &&
                vis1.ContainsKey(neighbor))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    // The shadowcasting beast
    private record struct LightJob() : IParallelRobustJob
    {
        public int BatchSize => 16; // basically 16 lights per thread

        public HashSet<Vector2i> Opaque = new();
        public List<LightSourceData> LightSources = new();
        public readonly List<Dictionary<Vector2i, int>> Vis1 = new();
        public readonly List<Dictionary<Vector2i, int>> Vis2 = new();
        public readonly List<HashSet<Vector2i>> SeedTiles = new();
        public readonly List<HashSet<Vector2i>> BoundaryTiles = new();
        public readonly List<Dictionary<Vector2i, float>> LocalResults = new();

        public void Execute(int index)
        {
            var source = LightSources[index];
            var vis1 = Vis1[index];
            var vis2 = Vis2[index];
            var seedTiles = SeedTiles[index];
            var boundary = BoundaryTiles[index];
            var results = LocalResults[index];

            vis1.Clear();
            vis2.Clear();
            seedTiles.Clear();
            boundary.Clear();
            results.Clear();

            var eyePos = source.Tile;
            var range = (int)Math.Ceiling(source.Radius);

            if (!source.CastShadows)
            {
                for (var x = -range; x <= range; x++)
                {
                    for (var y = -range; y <= range; y++)
                    {
                        var tile = eyePos + new Vector2i(x, y);
                        var dist = new Vector2(x, y).Length();

                        if (!IsWithinDirectionalCone(source, new Vector2i(x, y)))
                            continue;

                        var intensity = GetLightIntensity(source, dist);

                        if (intensity > 0.01f) // Anything less is basically dark anyway
                            results[tile] = intensity;
                    }
                }
                return;
            }

            for (var x = -range; x <= range; x++)
            {
                for (var y = -range; y <= range; y++)
                {
                    var tile = eyePos + new Vector2i(x, y);
                    seedTiles.Add(tile);
                }
            }

            vis1[eyePos] = 0;
            vis2[eyePos] = 0;

            for (var depth = 1; depth <= range; depth++)
            {
                for (var x = -depth; x <= depth; x++)
                {
                    var bottomTile = eyePos + new Vector2i(x, -depth);
                    if (CheckNeighborsVis(vis2, Opaque, bottomTile, depth - 1))
                    {
                        vis2[bottomTile] = Opaque.Contains(bottomTile) ? -1 : depth;
                    }

                    var topTile = eyePos + new Vector2i(x, depth);
                    if (CheckNeighborsVis(vis2, Opaque, topTile, depth - 1))
                    {
                        vis2[topTile] = Opaque.Contains(topTile) ? -1 : depth;
                    }
                }

                for (var y = -depth + 1; y < depth; y++)
                {
                    var leftTile = eyePos + new Vector2i(-depth, y);
                    if (CheckNeighborsVis(vis2, Opaque, leftTile, depth - 1))
                    {
                        vis2[leftTile] = Opaque.Contains(leftTile) ? -1 : depth;
                    }

                    var rightTile = eyePos + new Vector2i(depth, y);
                    if (CheckNeighborsVis(vis2, Opaque, rightTile, depth - 1))
                    {
                        vis2[rightTile] = Opaque.Contains(rightTile) ? -1 : depth;
                    }
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

                    var topTile = eyePos + new Vector2i(x, yAbs);
                    if (CheckNeighborsVis(vis1, Opaque, topTile, depth - 1))
                    {
                        if (Opaque.Contains(topTile))
                            vis1[topTile] = -1;
                        else if (vis2.GetValueOrDefault(topTile) != 0)
                            vis1[topTile] = depth;
                    }

                    if (yAbs == 0)
                        continue;

                    var bottomTile = eyePos + new Vector2i(x, -yAbs);
                    if (CheckNeighborsVis(vis1, Opaque, bottomTile, depth - 1))
                    {
                        if (Opaque.Contains(bottomTile))
                            vis1[bottomTile] = -1;
                        else if (vis2.GetValueOrDefault(bottomTile) != 0)
                            vis1[bottomTile] = depth;
                    }
                }
            }

            foreach (var tile in seedTiles)
            {
                if (!Opaque.Contains(tile))
                    continue;

                if (vis1.ContainsKey(tile))
                    continue;

                if (HasVisibleFace(seedTiles, Opaque, vis1, tile, eyePos))
                {
                    boundary.Add(tile);
                }
            }

            foreach (var tile in boundary)
            {
                vis1[tile] = -1;
            }

            
            foreach (var tile in seedTiles)
            {
                if (!vis1.TryGetValue(tile, out _))
                    continue; 

                var delta = tile - eyePos;
                var dist = new Vector2(delta.X, delta.Y).Length();

                if (!IsWithinDirectionalCone(source, delta))
                    continue;

                var intensity = GetLightIntensity(source, dist);

                if (intensity > 0.01f)
                    results[tile] = intensity;
            }

            // The light source tile itself
            if (!results.ContainsKey(eyePos))
                results[eyePos] = source.Brightness;
        }
    }
}
