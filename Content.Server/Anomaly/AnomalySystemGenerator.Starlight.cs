using Content.Server.Anomaly.Components;
using Content.Shared.CCVar;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Anomaly;

public sealed partial class AnomalySystem
{
    /// <summary>
    /// Spawns an anomaly at a valid random location on the grid.
    /// </summary>
    public EntityUid? SpawnOnRandomGridLocationReturning(Entity<MapGridComponent?> grid, EntProtoId toSpawn)
    {
        if (!Resolve(grid.Owner, ref grid.Comp) || !TryGetRandomAnomalySpawnCoordinates(grid, out var targetCoords))
            return null;

        return Spawn(toSpawn, targetCoords);
    }

    private bool TryGetRandomAnomalySpawnCoordinates(Entity<MapGridComponent?> grid, out EntityCoordinates targetCoords)
    {
        if (grid.Comp is not { } gridComp)
        {
            targetCoords = EntityCoordinates.Invalid;
            return false;
        }

        var xform = Transform(grid.Owner);
        targetCoords = EntityCoordinates.Invalid;
        var gridBounds = gridComp.LocalAABB.Scale(_configuration.GetCVar(CCVars.AnomalyGenerationGridBoundsScale));

        for (var i = 0; i < 25; i++)
        {
            var randomX = Random.Next((int) gridBounds.Left, (int) gridBounds.Right);
            var randomY = Random.Next((int) gridBounds.Bottom, (int) gridBounds.Top);
            var tile = new Vector2i(randomX, randomY);

            if (_atmosphere.IsTileSpace(grid.Owner, xform.MapUid, tile) ||
                _atmosphere.IsTileAirBlockedCached(grid.Owner, tile))
                continue;

            var physQuery = GetEntityQuery<PhysicsComponent>();
            var valid = true;
            foreach (var ent in _mapSystem.GetAnchoredEntities(grid.Owner, gridComp, tile))
            {
                if (!physQuery.TryGetComponent(ent, out var body))
                    continue;
                if (body.BodyType != BodyType.Static ||
                    !body.Hard ||
                    (body.CollisionLayer & (int) CollisionGroup.Impassable) == 0)
                    continue;

                valid = false;
                break;
            }
            if (!valid)
                continue;

            var pos = _mapSystem.GridTileToLocal(grid.Owner, gridComp, tile);
            var mapPos = _transform.ToMapCoordinates(pos);
            var antiAnomalyZones = AllEntityQuery<AntiAnomalyZoneComponent, TransformComponent>();
            while (antiAnomalyZones.MoveNext(out _, out var zone, out var antiXform))
            {
                if (antiXform.MapID != mapPos.MapId)
                    continue;

                var delta = _transform.GetWorldPosition(antiXform) - mapPos.Position;
                if (delta.LengthSquared() < zone.ZoneRadius * zone.ZoneRadius)
                {
                    valid = false;
                    break;
                }
            }
            if (!valid)
                continue;

            targetCoords = pos;
            return true;
        }

        return false;
    }
}
