using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Station.Events;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Salvage.VGRoid;

/// <summary>
/// A self-heal PATCH for VGroid spawning so admins aren't bombarded with fix AHelps. With some helpful logging. (God I hope this at least helps)
/// Remove this once(if) VGroid actually gets fixed.
/// </summary>
public sealed class VGRoidSpawnValidationSystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string VGRoidPrototype = "VGRoid";

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("vgroid.spawn");

        SubscribeLocalEvent<StationDataComponent, StationPostInitEvent>(OnStationPostInit);
        SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnGridMapInit);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnStationPostInit(EntityUid uid, StationDataComponent component, ref StationPostInitEvent args)
        => ValidateAllVGRoids("station post-init");

    private void OnGridMapInit(EntityUid uid, MapGridComponent component, MapInitEvent args)
    {
        if (!IsVGRoid(uid))
            return;

        ValidateAllVGRoids("VGRoid grid map-init");
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.New != GameRunLevel.InRound)
            return;

        ValidateAllVGRoids("round start");
    }

    private void ValidateAllVGRoids(string reason)
    {
        var stations = CollectStationGrids();
        if (stations.Count == 0)
        {
            _sawmill.Warning($"Unable to validate VGRoid placement during {reason}: no station grids found.");
            return;
        }

        var foundAny = false;
        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var grid, out var xform, out var meta))
        {
            var hasMarker = TryComp(uid, out VGRoidSpawnMarkerComponent? marker);
            if (!hasMarker && !IsVGRoid(uid, meta))
                continue;

            foundAny = true;
            ValidateVGRoid(uid, grid, xform, meta, marker, stations, reason);
        }

        if (!foundAny)
            _sawmill.Warning($"No VGRoid grid found during {reason}.");
    }

    private void ValidateVGRoid(
        EntityUid uid,
        MapGridComponent grid,
        TransformComponent xform,
        MetaDataComponent meta,
        VGRoidSpawnMarkerComponent? marker,
        List<StationGridInfo> stations,
        string reason)
    {
        var vgroidInfo = GetGridInfo(uid, grid, xform);
        var target = PickTargetStation(vgroidInfo, stations);
        if (target == null)
        {
            _sawmill.Warning($"Unable to validate {ToPrettyString(uid)} during {reason}: no usable target station grid found.");
            return;
        }

        if (marker == null)
        {
            _sawmill.Warning(
                $"Unable to validate {ToPrettyString(uid)} during {reason}: VGRoid grid has no VGRoidSpawnMarker component. " +
                "Metadata fallback recognized it, but no configured distance range is available.");
            return;
        }

        if (marker.MaximumEdgeDistance <= 0f || marker.MaximumEdgeDistance < marker.MinimumEdgeDistance)
        {
            _sawmill.Error(
                $"Unable to validate {ToPrettyString(uid)} during {reason}: invalid VGRoidSpawnMarker range " +
                $"{marker.MinimumEdgeDistance:F0}-{marker.MaximumEdgeDistance:F0}. " +
                "The marker should be populated from the DungeonSpawnGroup distance config when spawned.");
            return;
        }

        var targetInfo = target.Value;
        var centerDistance = Vector2.Distance(vgroidInfo.Center, targetInfo.Center);
        var edgeDistance = GetEdgeDistance(centerDistance, targetInfo.Radius, vgroidInfo.Radius);
        var wrongMap = vgroidInfo.MapId != targetInfo.MapId;
        var tooClose = edgeDistance < marker.MinimumEdgeDistance - marker.DistanceTolerance;
        var tooFar = edgeDistance > marker.MaximumEdgeDistance + marker.DistanceTolerance;

        _sawmill.Info(
            $"VGRoid spawn check ({reason}): grid={ToPrettyString(uid)} proto={meta.EntityPrototype?.ID ?? "<none>"} " +
            $"station={ToPrettyString(targetInfo.Station)} stationGrid={ToPrettyString(targetInfo.Grid)} " +
            $"actualMap={vgroidInfo.MapId} expectedMap={targetInfo.MapId} " +
            $"centerDistance={centerDistance:F1} edgeDistance={edgeDistance:F1} " +
            $"expectedEdgeDistance={marker.MinimumEdgeDistance:F0}-{marker.MaximumEdgeDistance:F0} " +
            $"vgroidCenter={vgroidInfo.Center} stationCenter={targetInfo.Center}");

        if (!wrongMap && !tooClose && !tooFar)
            return;

        _sawmill.Error(
            $"VGRoid spawned outside the configured range ({reason}): grid={ToPrettyString(uid)} " +
            $"actualMap={vgroidInfo.MapId} expectedMap={targetInfo.MapId} " +
            $"centerDistance={centerDistance:F1} edgeDistance={edgeDistance:F1} " +
            $"expectedEdgeDistance={marker.MinimumEdgeDistance:F0}-{marker.MaximumEdgeDistance:F0}. Repositioning.");

        RepositionVGRoid(uid, grid, xform, targetInfo, marker);
    }

    private void RepositionVGRoid(
        EntityUid uid,
        MapGridComponent grid,
        TransformComponent xform,
        StationGridInfo target,
        VGRoidSpawnMarkerComponent marker)
    {
        var mapUid = _map.GetMapOrInvalid(target.MapId);
        if (mapUid == EntityUid.Invalid)
        {
            _sawmill.Error($"Unable to reposition {ToPrettyString(uid)}: expected map {target.MapId} has no map entity.");
            return;
        }

        var theta = _random.NextFloat(0f, MathF.PI * 2f);
        var direction = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
        var edgeDistance = _random.NextFloat(marker.MinimumEdgeDistance, marker.MaximumEdgeDistance);
        var centerDistance = edgeDistance + target.Radius + GetGridRadius(grid);
        var desiredCenter = target.Center + (direction * centerDistance);

        // Store the grid with zero rotation. Dungeon/asteroid grids do not need a preserved random rotation,
        // and using zero here makes the center correction deterministic.
        var desiredOrigin = desiredCenter - grid.LocalAABB.Center;

        _transform.SetParent(uid, xform, mapUid);
        _transform.SetWorldPositionRotation(uid, desiredOrigin, Angle.Zero, xform);

        var newInfo = GetGridInfo(uid, grid, xform);
        var newCenterDistance = Vector2.Distance(newInfo.Center, target.Center);
        var newEdgeDistance = GetEdgeDistance(newCenterDistance, target.Radius, newInfo.Radius);

        _sawmill.Info(
            $"Repositioned VGRoid: grid={ToPrettyString(uid)} map={newInfo.MapId} " +
            $"centerDistance={newCenterDistance:F1} edgeDistance={newEdgeDistance:F1} " +
            $"expectedEdgeDistance={marker.MinimumEdgeDistance:F0}-{marker.MaximumEdgeDistance:F0} " +
            $"newCenter={newInfo.Center} stationCenter={target.Center}");
    }

    private List<StationGridInfo> CollectStationGrids()
    {
        var stations = new List<StationGridInfo>();
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var stationUid, out var stationData))
        {
            var grids = stationData.MainGrids.Count > 0
                ? stationData.MainGrids
                : stationData.Grids;

            foreach (var gridUid in grids)
            {
                if (!TryComp(gridUid, out MapGridComponent? grid) ||
                    !TryComp(gridUid, out TransformComponent? xform) ||
                    xform.MapID == MapId.Nullspace)
                {
                    continue;
                }

                var info = GetGridInfo(gridUid, grid, xform);
                stations.Add(new StationGridInfo(stationUid, gridUid, info.MapId, info.Center, info.Radius));
            }
        }

        return stations;
    }

    private StationGridInfo? PickTargetStation(GridInfo vgroid, List<StationGridInfo> stations)
    {
        var sameMap = stations
            .Where(station => station.MapId == vgroid.MapId)
            .OrderBy(station => Vector2.DistanceSquared(station.Center, vgroid.Center))
            .FirstOrDefault();

        if (sameMap.Grid != EntityUid.Invalid)
            return sameMap;

        // If the VGRoid is on a map with no station at all, fall back to the first main station grid.
        // This is the path that catches the "spawned on map 90 instead of map 64" style failure.
        return stations.FirstOrDefault();
    }

    private GridInfo GetGridInfo(EntityUid uid, MapGridComponent grid, TransformComponent xform)
    {
        var center = Vector2.Transform(grid.LocalAABB.Center, _transform.GetWorldMatrix(xform));
        return new GridInfo(uid, xform.MapID, center, GetGridRadius(grid));
    }

    private static float GetGridRadius(MapGridComponent grid)
        => grid.LocalAABB.Size.Length() / 2f;

    private static float GetEdgeDistance(float centerDistance, float stationRadius, float vgroidRadius)
        => MathF.Max(0f, centerDistance - stationRadius - vgroidRadius);

    private bool IsVGRoid(EntityUid uid)
        => HasComp<VGRoidSpawnMarkerComponent>(uid)
            || TryComp(uid, out MetaDataComponent? meta) && IsVGRoid(uid, meta);

    private static bool IsVGRoid(EntityUid _, MetaDataComponent meta)
        => meta.EntityPrototype?.ID == VGRoidPrototype
            || meta.EntityName.Contains("vgroid", StringComparison.OrdinalIgnoreCase)
            || meta.EntityName.Contains("VGRoid", StringComparison.OrdinalIgnoreCase);

    private readonly record struct GridInfo(EntityUid Grid, MapId MapId, Vector2 Center, float Radius);

    private readonly record struct StationGridInfo(
        EntityUid Station,
        EntityUid Grid,
        MapId MapId,
        Vector2 Center,
        float Radius);
}
