using System.Numerics;
using Content.Shared._Starlight.Shadekin;
using Robust.Server.GameObjects;
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
    [Dependency] private readonly SharedMapSystem _maps = default!;
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, float>> _lightGrids = new();
    // Reused every tick so we dont murder GC
    private readonly HashSet<Entity<OccluderComponent>> _occluders = new();
    private EntityQuery<OccluderComponent> _occluderQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(1f);

    private readonly HashSet<Vector2i> _opaque = new();
    private readonly HashSet<Vector2i> _scanTiles = new();

    private LightJob _job; 

    private record struct LightSourceData(
        Vector2i Tile,
        float Radius,
        float Energy,
        bool CastShadows);

    public override void Initialize()
    {
        base.Initialize();
        _occluderQuery = GetEntityQuery<OccluderComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        _job = new LightJob
        {
            System = this,
        };
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

            // deal with disabled light with negative energy
            if (!lightComp.Enabled
                || lightComp.Radius < 1
                || lightComp.Energy <= 0)
                continue;

            var tile = _maps.LocalToTile(gridUid, gridData.Grid, xform.Coordinates);
            var sources = GetOrCreateBucket(lightSourcesByGrid, gridUid);

            sources.Add(new LightSourceData(
                tile,
                lightComp.Radius,
                lightComp.Energy,
                lightComp.CastShadows));
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
        foreach (var tile in _scanTiles)
        {
            if (IsOccluded(grid, tile))
                _opaque.Add(tile);
        }

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

    private bool IsOccluded(Entity<BroadphaseComponent, MapGridComponent> grid, Vector2i tile)
    {
        var tileBounds = _lookup.GetLocalBounds(tile, grid.Comp2.TileSize);
        _occluders.Clear();
        _lookup.GetLocalEntitiesIntersecting((grid.Owner, grid.Comp1), tileBounds, _occluders, query: _occluderQuery, flags: LookupFlags.Static | LookupFlags.Approximate);

        foreach (var occluder in _occluders)
        {
            if (!occluder.Comp.Enabled)
                continue;

            // Verify the occluder actually belongs to this tile, not a neighbor
            var xform = _xformQuery.GetComponent(occluder.Owner);
            var occTile = _maps.LocalToTile(grid.Owner, grid.Comp2, xform.Coordinates);
            if (occTile != tile)
                continue;

            return true;
        }

        return false;
    }

    // Why 3 idfk
    private const float MaxExposure = 3f;
    public float GetExposure(EntityUid uid) => Math.Clamp(GetFullExposure(uid) / MaxExposure, 0f, 1f);

    public float GetFullExposure(EntityUid uid)
    {
        var xform = Transform(uid);
        var gridUid = xform.GridUid;

        if (gridUid is null)
            return 0f;

        if (!_lightGrids.TryGetValue(gridUid.Value, out var lightMap))
            return 0f;

        if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
            return 0f;

        var tile = _maps.LocalToTile(gridUid.Value, gridComp, xform.Coordinates);
        return lightMap.GetValueOrDefault(tile, 0f);
    }
    public float GetTileLight(EntityUid gridUid, Vector2i tile)
    {
        if (!_lightGrids.TryGetValue(gridUid, out var lightMap))
            return 0f;

        return lightMap.GetValueOrDefault(tile, 0f);
    }

    #region Shadowcasting helpers
    private static int GetMaxDelta(Vector2i tile, Vector2i center)
    {
        var delta = tile - center;
        return Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
    }

    private static int GetSumDelta(Vector2i tile, Vector2i center)
    {
        var delta = tile - center;
        return Math.Abs(delta.X) + Math.Abs(delta.Y);
    }

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

        public required LightGridSystem System;

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

                        if (dist > source.Radius)
                            continue;

                        // Quadratic falloff is pretty enough and cheap enough for our needs
                        var denom = dist / source.Radius;
                        var attenuation = 1f - (denom * denom);
                        var intensity = source.Energy * attenuation * attenuation;

                        if (intensity > 0.01f) // Anything less is basically dark anyway
                            results[tile] = intensity;
                    }
                }
                return;
            }

            var maxDepthMax = 0;
            var sumDepthMax = 0;

            for (var x = -range; x <= range; x++)
            {
                for (var y = -range; y <= range; y++)
                {
                    var tile = eyePos + new Vector2i(x, y);
                    var xDelta = Math.Abs(x);
                    var yDelta = Math.Abs(y);

                    maxDepthMax = Math.Max(maxDepthMax, Math.Max(xDelta, yDelta));
                    sumDepthMax = Math.Max(sumDepthMax, xDelta + yDelta);
                    seedTiles.Add(tile);
                }
            }

            vis1[eyePos] = 0;
            vis2[eyePos] = 0;

            for (var d = 0; d < maxDepthMax; d++)
            {
                foreach (var tile in seedTiles)
                {
                    var maxDelta = GetMaxDelta(tile, eyePos);
                    if (maxDelta == d + 1 && CheckNeighborsVis(vis2, System._opaque, tile, d))
                    {
                        vis2[tile] = System._opaque.Contains(tile) ? -1 : d + 1;
                    }
                }
            }

            for (var d = 0; d < sumDepthMax; d++)
            {
                foreach (var tile in seedTiles)
                {
                    var sumDelta = GetSumDelta(tile, eyePos);
                    if (sumDelta == d + 1 && CheckNeighborsVis(vis1, System._opaque, tile, d))
                    {
                        if (System._opaque.Contains(tile))
                            vis1[tile] = -1;
                        else if (vis2.GetValueOrDefault(tile) != 0)
                            vis1[tile] = d + 1;
                    }
                }
            }

            foreach (var tile in seedTiles)
            {
                if (!System._opaque.Contains(tile))
                    continue;

                if (vis1.ContainsKey(tile))
                    continue;

                if (HasVisibleFace(seedTiles, System._opaque, vis1, tile, eyePos))
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

                if (dist > source.Radius)
                    continue;

                var denom = dist / source.Radius;
                var attenuation = 1f - (denom * denom);
                var intensity = source.Energy * attenuation * attenuation;

                if (intensity > 0.01f)
                    results[tile] = intensity;
            }

            // The light source tile itself
            if (!results.ContainsKey(eyePos))
                results[eyePos] = source.Energy;
        }
    }
}
