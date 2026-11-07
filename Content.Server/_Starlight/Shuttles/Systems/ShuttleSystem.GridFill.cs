using System.Numerics;
using Content.Server._Starlight.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

// ReSharper disable CheckNamespace
namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private bool TryEntitySpawn(Entity<MapGridComponent?> targetGrid, EntitySpawnGroup group, out EntityUid spawned)
    {
        spawned = EntityUid.Invalid;

        if (!_gridQuery.Resolve(targetGrid.Owner, ref targetGrid.Comp))
        {
            return false;
        }

        var entityProtoId = _random.Pick(group.Protos);

        var targetPhysics = _physicsQuery.Comp(targetGrid);

        var targetCenterCoords = new EntityCoordinates(targetGrid, targetPhysics.LocalCenter);
        var spawnCoords = targetCenterCoords;
        var distancePadding = MathF.Max(targetGrid.Comp.LocalAABB.Width, targetGrid.Comp.LocalAABB.Height);

        if (group.MinimumDistance > 0f)
        {
            spawnCoords = spawnCoords.Offset(_random.NextVector2(distancePadding + group.MinimumDistance, distancePadding + group.MaximumDistance));
        }

        var spawnMapCoords = _transform.ToMapCoordinates(spawnCoords);

        var spawnedEntity = Spawn(entityProtoId, spawnMapCoords);

        spawned = spawnedEntity;
        return true;
    }
}
