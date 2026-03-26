using Content.Shared.Coordinates;
using Content.Shared.EntityEffects;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Shared._Starlight.EntityEffects.Effects.Xenobiology;

public sealed partial class MutateIntoEntityEntityEffectSystem : EntityEffectSystem<MindContainerComponent, MutateIntoEntity>
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedMindSystem _sharedMindSystem = default!;
    
    protected override void Effect(Entity<MindContainerComponent> entity, ref EntityEffectEvent<MutateIntoEntity> args)
    {
        var proto = args.Effect.Entity;
        var newEntity = _entityManager.SpawnAtPosition(proto, entity.Owner.ToCoordinates());
        if (entity.Comp.Mind.HasValue)
            _sharedMindSystem.TransferTo(entity.Comp.Mind.Value, newEntity);
        PredictedQueueDel(entity);
    }
}