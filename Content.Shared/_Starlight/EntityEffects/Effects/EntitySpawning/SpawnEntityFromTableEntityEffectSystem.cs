using System.Numerics;
using Content.Shared.Coordinates;
using Content.Shared.EntityEffects;
using Content.Shared.EntityTable;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.EntityEffects.Effects.EntitySpawning;

/// <summary>
/// Spawns a number of entities from a given prototype at the coordinates of this entity.
/// Amount is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed class SpawnEntityFromTableEntityEffectSystem : EntityEffectSystem<TransformComponent, SpawnEntityFromTable>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    
    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<SpawnEntityFromTable> args)
    {
        var quantity = args.Effect.Number * (int)Math.Floor(args.Scale);
        var random = _robustRandom.GetRandom();
        
        if (_net.IsServer)
        {
            for (var i = 0; i < quantity; i++)
            {
                var spawns = _entityTable.GetSpawns(args.Effect.EntityTable, random);
                foreach (var proto in spawns)
                {
                    var randomOffset = new Vector2(random.NextFloat(-args.Effect.Offset, args.Effect.Offset), random.NextFloat(-args.Effect.Offset, args.Effect.Offset));
                    var ec = new EntityCoordinates(entity.Owner, entity.Owner.ToCoordinates().Position + randomOffset);
                    _entityManager.SpawnAtPosition(proto, ec);
                }
            }
        }
    }
}