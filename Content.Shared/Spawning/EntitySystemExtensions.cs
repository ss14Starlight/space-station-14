using System.Diagnostics.CodeAnalysis;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Spawning
{
    public static class EntitySystemExtensions
    {
        public static EntityUid? SpawnIfUnobstructed(
            this IEntityManager entityManager,
            string? prototypeName,
            EntityCoordinates coordinates,
            CollisionGroup collisionLayer,
            in Box2? box = null,
            EntityLookupSystem? lookup = null)
        {
            lookup ??= entityManager.System<EntityLookupSystem>();
            var mapCoordinates = entityManager.System<SharedTransformSystem>().ToMapCoordinates(coordinates);

            return entityManager.SpawnIfUnobstructed(prototypeName, mapCoordinates, collisionLayer, box, lookup);
        }

        public static EntityUid? SpawnIfUnobstructed(
            this IEntityManager entityManager,
            string? prototypeName,
            MapCoordinates coordinates,
            CollisionGroup collisionLayer,
            in Box2? box = null,
            EntityLookupSystem? lookup = null)
        {
            var boxOrDefault = box.GetValueOrDefault(Box2.UnitCentered).Translated(coordinates.Position);
            lookup ??= entityManager.System<EntityLookupSystem>();

            var entities = new HashSet<Entity<PhysicsComponent>>();
            lookup.GetEntitiesIntersecting(coordinates.MapId, boxOrDefault, entities);

            foreach (var (_, body) in entities)
            {
                if (!body.Hard)
                {
                    continue;
                }

                // TODO: wtf fix this
                if (collisionLayer == 0 || (body.CollisionMask & (int) collisionLayer) == 0)
                {
                    continue;
                }

                return null;
            }

            return entityManager.SpawnEntity(prototypeName, coordinates);
        }

        public static bool TrySpawnIfUnobstructed(
            this IEntityManager entityManager,
            string? prototypeName,
            EntityCoordinates coordinates,
            CollisionGroup collisionLayer,
            [NotNullWhen(true)] out EntityUid? entity,
            Box2? box = null,
            EntityLookupSystem? lookup = null)
        {
            entity = entityManager.SpawnIfUnobstructed(prototypeName, coordinates, collisionLayer, box, lookup);

            return entity != null;
        }

        public static bool TrySpawnIfUnobstructed(
            this IEntityManager entityManager,
            string? prototypeName,
            MapCoordinates coordinates,
            CollisionGroup collisionLayer,
            [NotNullWhen(true)] out EntityUid? entity,
            in Box2? box = null,
            EntityLookupSystem? lookup = null)
        {
            entity = entityManager.SpawnIfUnobstructed(prototypeName, coordinates, collisionLayer, box, lookup);

            return entity != null;
        }
    }
}
