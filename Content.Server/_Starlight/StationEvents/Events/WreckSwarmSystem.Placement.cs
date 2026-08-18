using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server._Starlight.StationEvents.Components;
using Content.Server.Salvage.Magnet;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._Starlight.StationEvents.Events;

public sealed partial class WreckSwarmSystem
{
    #region Constants

    private const int PlacementAttempts = 32;
    private const float MinApproachDistance = 100f;
    private const float MaxApproachDistance = 512f;
    /// <summary>
    /// Extra hull standoff as a fraction of station radius so huge stations still
    /// get a visible inbound track before impact.
    /// </summary>
    private const float ApproachStationScale = 0.5f;
    private const float SpawnPadding = 2f;
    /// <summary>
    /// Radial probe from a candidate spawn. Interior station holes and hulls closer
    /// than this fail the candidate.
    /// </summary>
    private const float NearbyClearance = 10f;
    private const int RadialClearanceRays = 16;
    private const float CorridorClearance = 2f;
    private const float RayOriginPadding = 8f;
    private const float LargeBodyMass = 50f;
    private const float LargeBodyExtent = 4f;
    private const int StructuralCollisionMask = (int)(CollisionGroup.MapGrid | CollisionGroup.Impassable);

    #endregion

    #region Methods

    private bool TryPlanLaunch(
        EntityUid station,
        StationDataComponent stationData,
        WreckSwarmComponent component,
        WreckFootprint footprint,
        [NotNullWhen(true)] out WreckLaunchPlan? plan)
    {
        plan = null;

        if (!TryGetStationAimPoint((station, stationData), out var targetCoords))
            return false;

        var targetMap = _transform.ToMapCoordinates(targetCoords);
        var mapId = targetMap.MapId;
        var targetWorld = targetMap.Position;
        var stationGrids = stationData.Grids;
        var stationAabb = GetCombinedStationAabb(stationGrids, mapId);
        if (stationAabb == null)
            return false;

        var stationRadius = MathF.Max((stationAabb.Value.TopRight - stationAabb.Value.Center).Length(), 1f);
        var approachDistance = GetApproachDistance(stationRadius);
        SnapshotMapGrids(mapId, stationGrids);

        for (var i = 0; i < PlacementAttempts; i++)
        {
            var spawnAngle = RobustRandom.NextAngle();
            var outward = RobustRandom.NextAngle().ToVec();
            var approachDir = -outward;

            var rayOrigin = targetWorld - approachDir * (stationRadius + RayOriginPadding);
            var rayLength = stationRadius + RayOriginPadding + 16f;
            var ray = new CollisionRay(rayOrigin, approachDir, StructuralCollisionMask);
            var hits = _physics.IntersectRayWithPredicate(
                mapId,
                ray,
                rayLength,
                IsIgnoredRayHit,
                returnOnFirstHit: true);

            RayCastResults? firstHit = null;
            foreach (var hit in hits)
            {
                firstHit = hit;
                break;
            }

            Vector2 hitPoint;
            if (firstHit is { } hitResult && BelongsToStation(hitResult.HitEntity, stationGrids))
            {
                hitPoint = hitResult.HitPos;
            }
            else if (firstHit == null &&
                     ray.Intersects(stationAabb.Value, out _, out hitPoint) &&
                     !RayBlockedByNonStationGrid(rayOrigin, hitPoint))
            {
                // No structural fixtures yet; still aim at the station hull.
            }
            else
            {
                continue;
            }

            var spawnCenter = hitPoint - approachDir * (approachDistance + footprint.Radius + SpawnPadding);
            var placements = BuildChildPlacements(footprint, spawnCenter, spawnAngle);
            if (SpawnBlockedByNearbyGeometry(mapId, spawnCenter) || SpawnIntersectsGrid(mapId, placements))
                continue;

            var flightDistance = (hitPoint - spawnCenter).Length();
            var flightTime = component.Velocity > 0.01f ? flightDistance / component.Velocity : 0f;
            var corridor = BuildCorridor(spawnCenter, hitPoint, footprint.Radius);
            if (CorridorBlocked(mapId, corridor, stationGrids, flightTime))
                continue;

            if (SpawnIntersectsLooseEntities(mapId, placements))
                continue;

            var launchVelocity = _physics.GetMapLinearVelocity(targetCoords) + approachDir * component.Velocity;
            plan = new WreckLaunchPlan(mapId, spawnCenter, approachDir, launchVelocity, placements);
            return true;
        }

        Sawmill.Info("Wreck swarm found no clear station approach; ending without spawn.");
        return false;
    }

    private bool TryGetStationAimPoint(Entity<StationDataComponent> station, out EntityCoordinates targetCoords)
    {
        if (TryFindRandomTileOnStation(station, out _, out _, out targetCoords))
            return true;

        // Interior-tile sampling can miss on tiny or atmos-less test grids; any real floor still aims at the station.
        foreach (var gridUid in station.Comp.Grids)
        {
            if (!TryComp(gridUid, out MapGridComponent? grid))
                continue;

            foreach (var tile in _mapSystem.GetAllTiles(gridUid, grid))
            {
                if (tile.Tile.IsEmpty)
                    continue;

                targetCoords = _mapSystem.GridTileToLocal(gridUid, grid, tile.GridIndices);
                return true;
            }
        }

        targetCoords = default;
        return false;
    }

    /// <summary>
    /// Salvage spawners emit loot and mobs as map children via SpawnAtPosition.
    /// Reparent them onto the wreck grid before the temp map is deleted, and keep
    /// their local velocity at zero so they ride the launch instead of being left in space.
    /// </summary>
    private void AttachLooseTempMapEntities(EntityUid wreckMap, WreckFootprint footprint)
    {
        if (footprint.Children.Count == 0)
            return;

        var wreckGrids = new HashSet<EntityUid>(footprint.Children.Count);
        foreach (var child in footprint.Children)
        {
            wreckGrids.Add(child.Grid);
        }

        var toAttach = new List<EntityUid>();
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.MapUid != wreckMap || uid == wreckMap)
                continue;

            if (wreckGrids.Contains(uid) || HasComp<MapComponent>(uid) || HasComp<MapGridComponent>(uid))
                continue;

            // Already a wreck-grid rider (anchored walls, attached biome ents).
            if (xform.GridUid is { } gridUid && wreckGrids.Contains(gridUid))
                continue;

            toAttach.Add(uid);
        }

        foreach (var entity in toAttach)
        {
            if (TerminatingOrDeleted(entity))
                continue;

            var xform = Transform(entity);
            var grid = GetAttachTargetGrid(xform, footprint);
            _transform.SetParent(entity, xform, grid);

            if (TryComp<SalvageMobRestrictionsComponent>(entity, out var restrictions))
                restrictions.LinkedEntity = grid;
        }
    }

    private EntityUid GetAttachTargetGrid(TransformComponent xform, WreckFootprint footprint)
    {
        if (xform.GridUid is { } gridUid)
        {
            foreach (var child in footprint.Children)
            {
                if (child.Grid == gridUid)
                    return gridUid;
            }
        }

        var worldPos = _transform.GetWorldPosition(xform);
        var nearest = footprint.Children[0].Grid;
        var best = float.MaxValue;
        foreach (var child in footprint.Children)
        {
            var dist = (worldPos - _transform.GetWorldPosition(child.Grid)).LengthSquared();
            if (dist >= best)
                continue;

            best = dist;
            nearest = child.Grid;
        }

        return nearest;
    }

    private void ApplyLaunch(EntityUid spawnMapUid, WreckLaunchPlan plan)
    {
        foreach (var child in plan.Children)
        {
            var xform = Transform(child.Grid);
            _transform.SetParent(child.Grid, xform, spawnMapUid);
            _transform.SetWorldPositionRotation(child.Grid, child.WorldPosition, child.WorldRotation, xform);

            if (!TryComp<PhysicsComponent>(child.Grid, out var physics))
                continue;

            _physics.SetLinearVelocity(child.Grid, plan.LaunchVelocity, body: physics);
            KeepRidersOnWreck(child.Grid);
        }
    }

    /// <summary>
    /// Unanchored mobs/items have their own physics. Zero their local velocity so they
    /// inherit the wreck's launch instead of remaining at rest in world space.
    /// </summary>
    private void KeepRidersOnWreck(EntityUid grid)
    {
        var enumerator = Transform(grid).ChildEnumerator;
        while (enumerator.MoveNext(out var rider))
        {
            if (HasComp<MapGridComponent>(rider) || !TryComp<PhysicsComponent>(rider, out var physics))
                continue;

            if (physics.BodyType == BodyType.Static)
                continue;

            // Local velocity is relative to the parent grid; zero it so map velocity matches the wreck.
            _physics.SetLinearVelocity(rider, Vector2.Zero, body: physics);
            _physics.SetAngularVelocity(rider, 0f, body: physics);
            _physics.SetAwake((rider, physics), true);
        }
    }

    private bool RayBlockedByNonStationGrid(Vector2 origin, Vector2 hitPoint)
    {
        var segment = Box2.FromTwoPoints(origin, hitPoint).Enlarged(0.5f);

        foreach (var snapshot in _gridSnapshots)
        {
            if (snapshot.IsStation)
                continue;

            if (segment.Intersects(snapshot.WorldAabb))
                return true;
        }

        return false;
    }

    private Box2? GetCombinedStationAabb(HashSet<EntityUid> stationGrids, MapId mapId)
    {
        Box2? combined = null;
        foreach (var gridUid in stationGrids)
        {
            var xform = Transform(gridUid);
            if (xform.MapID != mapId)
                continue;

            if (!TryComp(gridUid, out MapGridComponent? grid))
                continue;

            var aabb = GetGridWorldAabb(grid, xform);
            combined = combined == null ? aabb : combined.Value.Union(aabb);
        }

        return combined;
    }

    private void SnapshotMapGrids(MapId mapId, HashSet<EntityUid> stationGrids)
    {
        _gridSnapshots.Clear();
        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var grid, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var velocity = TryComp<PhysicsComponent>(uid, out var physics)
                ? physics.LinearVelocity
                : Vector2.Zero;
            _gridSnapshots.Add(new GridLaunchSnapshot(
                uid,
                GetGridWorldAabb(grid, xform),
                velocity,
                stationGrids.Contains(uid)));
        }
    }

    private static List<WreckChildPlacement> BuildChildPlacements(
        WreckFootprint footprint,
        Vector2 spawnCenter,
        Angle spawnAngle)
    {
        var placements = new List<WreckChildPlacement>(footprint.Children.Count);
        foreach (var child in footprint.Children)
        {
            var worldPos = spawnCenter + spawnAngle.RotateVec(child.LocalPosition - footprint.LocalCenter);
            var worldRot = spawnAngle + child.LocalRotation;
            var localBounds = child.LocalAABB.Enlarged(SpawnPadding);
            var bounds = new Box2Rotated(localBounds.Translated(worldPos), worldRot, worldPos);
            placements.Add(new WreckChildPlacement(child.Grid, worldPos, worldRot, bounds));
        }

        return placements;
    }

    private static Box2Rotated BuildCorridor(Vector2 spawnCenter, Vector2 hitPoint, float wreckRadius)
    {
        var along = hitPoint - spawnCenter;
        var length = along.Length();
        var angle = along.ToWorldAngle();
        var halfWidth = wreckRadius + CorridorClearance;
        var localBox = new Box2(0f, -halfWidth, length, halfWidth);
        return new Box2Rotated(localBox.Translated(spawnCenter), angle, spawnCenter);
    }

    private Box2 GetGridWorldAabb(MapGridComponent grid, TransformComponent xform)
    {
        return _transform.GetWorldMatrix(xform).TransformBox(grid.LocalAABB);
    }

    /// <summary>
    /// Stand off far enough to enter default shuttle radar with warning time.
    /// Huge stations add extra distance so the wreck is not glued to the hull blob.
    /// </summary>
    private static float GetApproachDistance(float stationRadius)
    {
        var desired = MathF.Max(SharedRadarConsoleSystem.DefaultMaxRange, stationRadius * ApproachStationScale);
        return Math.Clamp(desired, MinApproachDistance, MaxApproachDistance);
    }

    /// <summary>
    /// Rejects candidates with structure inside <see cref="NearbyClearance"/>, including
    /// interior station voids whose tiles never overlap the wreck footprint.
    /// </summary>
    private bool SpawnBlockedByNearbyGeometry(MapId mapId, Vector2 spawnCenter)
    {
        for (var i = 0; i < RadialClearanceRays; i++)
        {
            var dir = new Angle(MathF.Tau * i / RadialClearanceRays).ToVec();
            var ray = new CollisionRay(spawnCenter, dir, StructuralCollisionMask);
            var hits = _physics.IntersectRayWithPredicate(
                mapId,
                ray,
                NearbyClearance,
                IsIgnoredRayHit,
                returnOnFirstHit: true);

            foreach (var _ in hits)
            {
                return true;
            }
        }

        return false;
    }

    private bool SpawnIntersectsGrid(MapId mapId, List<WreckChildPlacement> placements)
    {
        foreach (var child in placements)
        {
            var aabb = child.Bounds.CalcBoundingBox();
            foreach (var snapshot in _gridSnapshots)
            {
                if (aabb.Intersects(snapshot.WorldAabb))
                    return true;
            }

            _intersectingGrids.Clear();
            _mapManager.FindGridsIntersecting(mapId, child.Bounds, ref _intersectingGrids, approx: false, includeMap: false);
            if (_intersectingGrids.Count > 0)
                return true;
        }

        return false;
    }

    private bool CorridorBlocked(
        MapId mapId,
        Box2Rotated corridor,
        HashSet<EntityUid> stationGrids,
        float flightTime)
    {
        var corridorAabb = corridor.CalcBoundingBox();
        foreach (var snapshot in _gridSnapshots)
        {
            if (snapshot.IsStation || snapshot.LinearVelocity.LengthSquared() < 0.0001f)
                continue;

            // Stationary overlap is handled by the precise rotated query below.
            // Moving grids are swept conservatively because FindGridsIntersecting is current-pose only.
            var swept = snapshot.WorldAabb.Union(snapshot.WorldAabb.Translated(snapshot.LinearVelocity * flightTime));
            if (corridorAabb.Intersects(swept) && !corridorAabb.Intersects(snapshot.WorldAabb))
                return true;
        }

        _intersectingGrids.Clear();
        _mapManager.FindGridsIntersecting(mapId, corridor, ref _intersectingGrids, approx: false, includeMap: false);
        foreach (var grid in _intersectingGrids)
        {
            if (!stationGrids.Contains(grid.Owner))
                return true;
        }

        foreach (var entity in _lookup.GetEntitiesIntersecting(mapId, corridor, LookupFlags.Uncontained))
        {
            if (IsIgnoredCorridorEntity(entity, stationGrids))
                continue;

            if (IsLargeHardBody(entity))
                return true;
        }

        return false;
    }

    private bool SpawnIntersectsLooseEntities(MapId mapId, List<WreckChildPlacement> placements)
    {
        foreach (var child in placements)
        {
            foreach (var entity in _lookup.GetEntitiesIntersecting(mapId, child.Bounds, LookupFlags.Uncontained))
            {
                if (IsBlockingSpawnEntity(entity))
                    return true;
            }
        }

        return false;
    }

    private bool IsIgnoredRayHit(EntityUid hit)
    {
        return HasComp<MapComponent>(hit);
    }

    private bool BelongsToStation(EntityUid hit, HashSet<EntityUid> stationGrids)
    {
        if (stationGrids.Contains(hit))
            return true;

        var gridUid = Transform(hit).GridUid;
        return gridUid != null && stationGrids.Contains(gridUid.Value);
    }

    private bool IsIgnoredCorridorEntity(EntityUid entity, HashSet<EntityUid> stationGrids)
    {
        if (HasComp<MapComponent>(entity) || stationGrids.Contains(entity))
            return true;

        var gridUid = Transform(entity).GridUid;
        return gridUid != null && stationGrids.Contains(gridUid.Value);
    }

    private bool IsBlockingSpawnEntity(EntityUid entity)
    {
        if (HasComp<MapComponent>(entity) || HasComp<MapGridComponent>(entity))
            return false;

        if (HasComp<ItemComponent>(entity) || HasComp<MobStateComponent>(entity))
            return true;

        return TryComp<PhysicsComponent>(entity, out var physics) && physics.CanCollide;
    }

    private bool IsLargeHardBody(EntityUid entity)
    {
        if (HasComp<MapComponent>(entity) || HasComp<MapGridComponent>(entity))
            return false;

        if (!TryComp<PhysicsComponent>(entity, out var physics) || !physics.Hard || !physics.CanCollide)
            return false;

        if (physics.FixturesMass >= LargeBodyMass)
            return true;

        var aabb = _physics.GetWorldAABB(entity);
        return MathF.Max(aabb.Width, aabb.Height) >= LargeBodyExtent;
    }

    #endregion

    #region Nested Types

    internal sealed record WreckGridInfo(
        EntityUid Grid,
        Vector2 LocalPosition,
        Angle LocalRotation,
        Box2 LocalAABB);

    internal sealed record WreckFootprint(
        List<WreckGridInfo> Children,
        Vector2 LocalCenter,
        float Radius);

    internal sealed record WreckChildPlacement(
        EntityUid Grid,
        Vector2 WorldPosition,
        Angle WorldRotation,
        Box2Rotated Bounds);

    internal sealed record WreckLaunchPlan(
        MapId MapId,
        Vector2 SpawnCenter,
        Vector2 ApproachDirection,
        Vector2 LaunchVelocity,
        List<WreckChildPlacement> Children);

    private readonly record struct GridLaunchSnapshot(
        EntityUid Uid,
        Box2 WorldAabb,
        Vector2 LinearVelocity,
        bool IsStation);

    #endregion
}
