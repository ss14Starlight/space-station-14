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

    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<SpawnAttachedTo> args)
    {
        var quantity = args.Effect.Number * (int)Math.Floor(args.Scale);
        var proto = args.Effect.Entity;
        EntityUid ent = entity.Owner;

        // don't actually use SpawnAttachedTo() because that guesses the parent entity -- we already have that information so we can do it the more reliable way:

        if (args.Effect.Predicted)
        {
            for (var i = 0; i < quantity; i++)
            {
                var new_ent = PredictedSpawnAtPosition(proto, ent.ToCoordinates());
                _transform.SetParent(new_ent, ent);
            }
        }
        else if (_net.IsServer)
        {
            for (var i = 0; i < quantity; i++)
            {
                var new_ent = SpawnAtPosition(proto, ent.ToCoordinates());
                _transform.SetParent(new_ent, ent);
            }
        }
    }
}

/// <inheritdoc cref="BaseSpawnEntityEntityEffect{T}"/>
public sealed partial class SpawnAttachedTo : BaseSpawnEntityEntityEffect<SpawnAttachedTo>;
