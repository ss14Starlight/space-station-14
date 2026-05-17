using System.Linq;
using System.Numerics;
using Content.Server._Starlight.Procedural.Events;
using Content.Server.GameTicking;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Salvage.VGRoid;

/// <summary>
/// A self-heal PATCH for VGroid spawning so admins aren't bombarded with fix AHelps. With some helpful logging. (God I hope this at least helps)
/// Remove this once(if) VGroid actually gets fixed.
/// </summary>
public sealed partial class VGRoidSpawnValidationSystem : EntitySystem
{
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const string VGRoidPrototype = "VGRoid";
    private const int PlacementAttempts = 32;
    private const float GridOverlapPadding = 32f;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("vgroid.spawn");

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<VGRoidSpawnMarkerComponent, DungeonGeneratedEvent>(OnDungeonGenerated);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.New != GameRunLevel.InRound)
            return;

        ValidateAllVGRoids("round start");
    }

    private void OnDungeonGenerated(EntityUid uid, VGRoidSpawnMarkerComponent marker, DungeonGeneratedEvent args)
    {
        marker.GenerationComplete = true;

        if (!TryComp(uid, out MapGridComponent? grid))
        {
            marker.PlacementComplete = true;

            _sawmill.Error(
                $"Unable to finalize generated VGRoid placement for {ToPrettyString(uid)}: " +
                "missing MapGridComponent after dungeon generation completed.");

            return;
        }

        if (!TryComp(uid, out TransformComponent? xform))
        {
            marker.PlacementComplete = true;

            _sawmill.Error(
                $"Unable to finalize generated VGRoid placement for {ToPrettyString(uid)}: " +
                "missing TransformComponent after dungeon generation completed.");

            return;
        }

        if (!TryComp(uid, out MetaDataComponent? meta))
        {
            marker.PlacementComplete = true;

            _sawmill.Error(
                $"Unable to finalize generated VGRoid placement for {ToPrettyString(uid)}: " +
                "missing MetaDataComponent after dungeon generation completed.");

            return;
        }

        var stations = CollectStationGrids();
        if (stations.Count == 0)
        {
            _sawmill.Error(
                $"Unable to finalize generated VGRoid placement for {ToPrettyString(uid)}: " +
                "no station grids found. Leaving placement pending for a later validation retry.");

            return;
        }

        ValidateVGRoid(uid, grid, xform, meta, marker, stations, $"dungeon generated seed={args.Seed}");
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

        if (!marker.GenerationComplete)
        {
            _sawmill.Info(
                $"Skipping VGRoid spawn check ({reason}): grid={ToPrettyString(uid)} " +
                "dungeon generation has not completed yet.");
            return;
        }

        var targetInfo = target.Value;

        if (!marker.PlacementComplete)
        {
            if (!TryFinalizeGeneratedVGRoidPlacement(uid, grid, xform, targetInfo, marker, vgroidInfo, reason))
                return;

            vgroidInfo = GetGridInfo(uid, grid, xform);
        }

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

        if (!TryPickClearVGRoidPlacement(uid, grid, target, marker, direction, out var desiredOrigin, out var desiredCenter))
        {
            _sawmill.Error(
                $"Unable to reposition {ToPrettyString(uid)} cleanly: could not find a clear position after {PlacementAttempts} attempts. " +
                "Falling back to random direction; VGRoid may overlap another grid.");

            var edgeDistance = _random.NextFloat(marker.MinimumEdgeDistance, marker.MaximumEdgeDistance);
            var centerDistance = edgeDistance + target.Radius + GetGridRadius(grid);
            desiredCenter = target.Center + (direction * centerDistance);
            desiredOrigin = desiredCenter - grid.LocalAABB.Center;
        }

        _transform.SetParent(uid, xform, mapUid);
        _transform.SetWorldPositionRotation(uid, desiredOrigin, Angle.Zero, xform);

        marker.GenerationComplete = true;
        marker.PlacementComplete = true;

        var newInfo = GetGridInfo(uid, grid, xform);
        var newCenterDistance = Vector2.Distance(newInfo.Center, target.Center);
        var newEdgeDistance = GetEdgeDistance(newCenterDistance, target.Radius, newInfo.Radius);

        _sawmill.Info(
            $"Repositioned VGRoid: grid={ToPrettyString(uid)} map={newInfo.MapId} " +
            $"centerDistance={newCenterDistance:F1} edgeDistance={newEdgeDistance:F1} " +
            $"expectedEdgeDistance={marker.MinimumEdgeDistance:F0}-{marker.MaximumEdgeDistance:F0} " +
            $"newCenter={newInfo.Center} stationCenter={target.Center}");
    }

    private bool TryPickClearVGRoidPlacement(
        EntityUid uid,
        MapGridComponent grid,
        StationGridInfo target,
        VGRoidSpawnMarkerComponent marker,
        Vector2 preferredDirection,
        out Vector2 desiredOrigin,
        out Vector2 desiredCenter)
    {
        desiredOrigin = default;
        desiredCenter = default;

        if (preferredDirection.LengthSquared() < 0.001f)
        {
            var theta = _random.NextFloat(0f, MathF.PI * 2f);
            preferredDirection = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
        }
        else
        {
            preferredDirection = Vector2.Normalize(preferredDirection);
        }

        for (var i = 0; i < PlacementAttempts; i++)
        {
            Vector2 direction;

            if (i == 0)
            {
                direction = preferredDirection;
            }
            else
            {
                // Spread retries around the preferred direction instead of always choosing
                // a totally unrelated side of the station.
                var angle = MathF.Atan2(preferredDirection.Y, preferredDirection.X);
                angle += _random.NextFloat(-MathF.PI / 2f, MathF.PI / 2f);

                direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }

            var edgeDistance = _random.NextFloat(marker.MinimumEdgeDistance, marker.MaximumEdgeDistance);
            var centerDistance = edgeDistance + target.Radius + GetGridRadius(grid);

            desiredCenter = target.Center + (direction * centerDistance);
            desiredOrigin = desiredCenter - grid.LocalAABB.Center;

            if (!WouldOverlapOtherGrid(uid, grid, target.MapId, desiredOrigin))
                return true;
        }

        return false;
    }

    private bool WouldOverlapOtherGrid(
        EntityUid uid,
        MapGridComponent grid,
        MapId mapId,
        Vector2 desiredOrigin)
    {
        var desiredBounds = new Box2(
            desiredOrigin + grid.LocalAABB.BottomLeft,
            desiredOrigin + grid.LocalAABB.TopRight).Enlarged(GridOverlapPadding);

        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var otherUid, out var otherGrid, out var otherXform))
        {
            if (otherUid == uid ||
                otherXform.MapID != mapId ||
                otherXform.MapID == MapId.Nullspace)
            {
                continue;
            }

            var otherCenter = Vector2.Transform(otherGrid.LocalAABB.Center, _transform.GetWorldMatrix(otherXform));
            var otherBounds = new Box2(
                otherCenter - (otherGrid.LocalAABB.Size / 2f),
                otherCenter + (otherGrid.LocalAABB.Size / 2f)).Enlarged(GridOverlapPadding);

            if (!desiredBounds.Intersects(otherBounds))
                continue;

            _sawmill.Info(
                $"Rejected VGRoid placement candidate: grid={ToPrettyString(uid)} " +
                $"would overlap {ToPrettyString(otherUid)} desiredCenter={desiredBounds.Center} " +
                $"otherCenter={otherBounds.Center}");

            return true;
        }

        return false;
    }

    private bool TryFinalizeGeneratedVGRoidPlacement(
        EntityUid uid,
        MapGridComponent grid,
        TransformComponent xform,
        StationGridInfo target,
        VGRoidSpawnMarkerComponent marker,
        GridInfo vgroidInfo,
        string reason)
    {
        var mapUid = _map.GetMapOrInvalid(target.MapId);
        if (mapUid == EntityUid.Invalid)
        {
            _sawmill.Error($"Unable to finalize generated VGRoid placement for {ToPrettyString(uid)}: expected map {target.MapId} has no map entity.");
            return false;
        }

        var direction = vgroidInfo.Center - target.Center;

        if (direction.LengthSquared() < 0.001f)
        {
            var theta = _random.NextFloat(0f, MathF.PI * 2f);
            direction = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
        }
        else
        {
            direction = Vector2.Normalize(direction);
        }

        if (!TryPickClearVGRoidPlacement(uid, grid, target, marker, direction, out var desiredOrigin, out var desiredCenter))
        {
            _sawmill.Error(
                $"Unable to finalize generated VGRoid placement for {ToPrettyString(uid)}: " +
                $"could not find a clear position after {PlacementAttempts} attempts. " +
                "Falling back to preferred direction; VGRoid may overlap another grid.");

            var edgeDistance = _random.NextFloat(marker.MinimumEdgeDistance, marker.MaximumEdgeDistance);
            var centerDistance = edgeDistance + target.Radius + GetGridRadius(grid);
            desiredCenter = target.Center + (Vector2.Normalize(direction) * centerDistance);
            desiredOrigin = desiredCenter - grid.LocalAABB.Center;
        }

        _transform.SetParent(uid, xform, mapUid);
        _transform.SetWorldPositionRotation(uid, desiredOrigin, Angle.Zero, xform);

        marker.GenerationComplete = true;
        marker.PlacementComplete = true;

        var newInfo = GetGridInfo(uid, grid, xform);
        var newCenterDistance = Vector2.Distance(newInfo.Center, target.Center);
        var newEdgeDistance = GetEdgeDistance(newCenterDistance, target.Radius, newInfo.Radius);

        _sawmill.Info(
            $"Finalized generated VGRoid placement ({reason}): grid={ToPrettyString(uid)} map={newInfo.MapId} " +
            $"centerDistance={newCenterDistance:F1} edgeDistance={newEdgeDistance:F1} " +
            $"expectedEdgeDistance={marker.MinimumEdgeDistance:F0}-{marker.MaximumEdgeDistance:F0} " +
            $"newCenter={newInfo.Center} stationCenter={target.Center}");

        return true;
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

    public void PushGridOutOfCompletedVGRoids(EntityUid uid)
    {
        if (HasComp<VGRoidSpawnMarkerComponent>(uid))
            return;

        if (!TryComp(uid, out MapGridComponent? grid) ||
            !TryComp(uid, out TransformComponent? xform) ||
            xform.MapID == MapId.Nullspace)
        {
            return;
        }

        var gridInfo = GetGridInfo(uid, grid, xform);

        var query = EntityQueryEnumerator<VGRoidSpawnMarkerComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var vgroidUid, out var marker, out var vgroidGrid, out var vgroidXform))
        {
            if (!marker.GenerationComplete ||
                !marker.PlacementComplete ||
                vgroidXform.MapID != gridInfo.MapId)
            {
                continue;
            }

            var vgroidInfo = GetGridInfo(vgroidUid, vgroidGrid, vgroidXform);
            var centerDistance = Vector2.Distance(gridInfo.Center, vgroidInfo.Center);
            var minimumDistance = gridInfo.Radius + vgroidInfo.Radius + GridOverlapPadding;

            if (centerDistance >= minimumDistance)
                continue;

            var direction = gridInfo.Center - vgroidInfo.Center;
            if (direction.LengthSquared() < 0.001f)
            {
                var theta = _random.NextFloat(0f, MathF.PI * 2f);
                direction = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
            }
            else
            {
                direction = Vector2.Normalize(direction);
            }

            var desiredCenter = vgroidInfo.Center + (direction * minimumDistance);
            var desiredOrigin = desiredCenter - grid.LocalAABB.Center;

            var mapUid = _map.GetMapOrInvalid(gridInfo.MapId);
            if (mapUid == EntityUid.Invalid)
                return;

            _transform.SetParent(uid, xform, mapUid);
            _transform.SetWorldPositionRotation(uid, desiredOrigin, Angle.Zero, xform);

            _sawmill.Warning(
                $"Moved generated grid out of VGRoid overlap: grid={ToPrettyString(uid)} " +
                $"vgroid={ToPrettyString(vgroidUid)} oldCenter={gridInfo.Center} newCenter={desiredCenter} " +
                $"minimumDistance={minimumDistance:F1}");

            return;
        }
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
            || (TryComp(uid, out MetaDataComponent? meta) && IsVGRoid(uid, meta));

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
