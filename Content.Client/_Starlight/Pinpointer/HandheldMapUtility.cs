using Content.Shared._Starlight.Pinpointer;
using Content.Shared.Item;
using Content.Shared.Pinpointer;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Starlight.Pinpointer;

public static class HandheldMapUtility
{
    public static bool CanDisplayTargetMap(IEntityManager entityManager, EntityUid owner, EntityUid targetGrid,
        EntityUid? currentMapUid)
    {
        if (!entityManager.TryGetComponent<TransformComponent>(owner, out var ownerXform) ||
            !entityManager.TryGetComponent<MapGridComponent>(targetGrid, out var targetGridComponent))
        {
            return false;
        }

        if (!entityManager.HasComponent<ItemComponent>(owner))
            return true;

        var canKeepCurrentMapOffGrid = false;
        var limitDistanceMultiplier = 0f;
        if (entityManager.TryGetComponent<StationMapComponent>(owner, out var stationMap))
        {
            if (stationMap.TargetGrid is { } configuredTarget)
            {
                if (configuredTarget != targetGrid)
                    return false;

                return IsWithinRange(entityManager, ownerXform, targetGrid, targetGridComponent,
                    stationMap.LimitDistanceMultiplier);
            }

            if (stationMap.InitializeWithStation)
                return false;

            canKeepCurrentMapOffGrid = true;
            limitDistanceMultiplier = stationMap.LimitDistanceMultiplier;
        }

        if (ownerXform.GridUid != targetGrid &&
            !(canKeepCurrentMapOffGrid && ownerXform.GridUid == null && currentMapUid == targetGrid))
            return false;

        var isStationGrid = entityManager.HasComponent<StationMemberComponent>(targetGrid);
        var isShuttleGrid = !entityManager.HasComponent<MapComponent>(targetGrid) &&
                            !entityManager.HasComponent<HandheldMapSurfaceComponent>(targetGrid) &&
                            entityManager.HasComponent<ShuttleComponent>(targetGrid);

        return (isStationGrid || isShuttleGrid) &&
                IsWithinRange(entityManager, ownerXform, targetGrid, targetGridComponent, limitDistanceMultiplier);
    }

    private static bool IsWithinRange(IEntityManager entityManager, TransformComponent ownerXform,
        EntityUid targetGrid, MapGridComponent targetGridComponent, float limitDistanceMultiplier)
    {
        if (limitDistanceMultiplier <= 0f)
            return true;

        var maxDistance = Math.Max(targetGridComponent.LocalAABB.Width, targetGridComponent.LocalAABB.Height) * limitDistanceMultiplier;
        var transformSystem = entityManager.System<SharedTransformSystem>();
        var ownerCoordinates = transformSystem.ToMapCoordinates(ownerXform.Coordinates, logError: false);
        var targetCoordinates = transformSystem.ToMapCoordinates(
            new EntityCoordinates(targetGrid, targetGridComponent.LocalAABB.Center),
            logError: false);

        return ownerCoordinates.MapId != MapId.Nullspace && ownerCoordinates.InRange(targetCoordinates, maxDistance);
    }
}
