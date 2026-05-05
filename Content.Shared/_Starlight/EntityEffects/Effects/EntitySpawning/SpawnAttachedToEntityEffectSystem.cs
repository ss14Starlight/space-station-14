using Robust.Shared.Network;
using Content.Shared.Coordinates;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.EntitySpawning;

namespace Content.Shared._Starlight.EntityEffects.Effects.EntitySpawning;

/// <summary>
/// Spawns a number of entities of a given prototype at the coordinates of this entity and attaches them to it.
/// Amount is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class SpawnAttachedToEntityEffectSystem : EntityEffectSystem<TransformComponent, SpawnAttachedTo>
{
    [Dependency] private readonly INetManager _net = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<SpawnAttachedTo> args)
    {
        var quantity = args.Effect.Number * (int)Math.Floor(args.Scale);
        var proto = args.Effect.Entity;
        EntityUid ent = entity.Owner;

        if (args.Effect.Predicted)
        {
            for (var i = 0; i < quantity; i++)
            {
                PredictedSpawnAttachedTo(proto, ent.ToCoordinates());
            }
        }
        else if (_net.IsServer)
        {
            for (var i = 0; i < quantity; i++)
            {
                SpawnAttachedTo(proto, ent.ToCoordinates());
            }
        }
    }
}

/// <inheritdoc cref="BaseSpawnEntityEntityEffect{T}"/>
public sealed partial class SpawnAttachedTo : BaseSpawnEntityEntityEffect<SpawnAttachedTo>;
