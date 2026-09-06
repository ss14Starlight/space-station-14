using Content.Shared.EntityEffects;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.EntityEffects.Effects.EntitySpawning;

public sealed partial class SpawnEntityWithOffsetEffectSystem : EntityEffectSystem<TransformComponent, SpawnEntityWithOffsetEffect>
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private INetManager _net = default!;

    protected override void Effect(Entity<TransformComponent> entity,
        ref EntityEffectEvent<SpawnEntityWithOffsetEffect> args)
    {
        if (args.Effect.UseMapCoords)
        {
            var mapCoords = _xform.GetMapCoordinates(entity);
            mapCoords = mapCoords.Offset(args.Effect.Offset);

            for (var i = 0; i < args.Effect.Amount; i++)
                if (args.Effect.Predicted)
                    EntityManager.PredictedSpawn(args.Effect.Entity, mapCoords, args.Effect.Overrides,
                        float.RadiansToDegrees(args.Effect.Angle));
                else if (_net.IsServer)
                    Spawn(args.Effect.Entity, mapCoords, args.Effect.Overrides,
                        float.RadiansToDegrees(args.Effect.Angle));
            return;
        }

        var pos = Transform(entity).Coordinates;
        pos = pos.Offset(args.Effect.Offset);

        for (var i = 0; i < args.Effect.Amount; i++)
            if (args.Effect.Predicted)
            {
                var uid = PredictedSpawnAtPosition(args.Effect.Entity, pos, args.Effect.Overrides);
                _xform.SetLocalRotation(uid, float.RadiansToDegrees(args.Effect.Angle));
            }
            else if (_net.IsServer)
            {
                var uid = SpawnAtPosition(args.Effect.Entity, pos, args.Effect.Overrides);
                _xform.SetLocalRotation(uid, float.RadiansToDegrees(args.Effect.Angle));
            }
    }
}
