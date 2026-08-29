using Content.Shared._Starlight.Genetics.GeneticTraits.Components;
using Content.Shared._Starlight.Genetics.GeneticTraits.Parts;
using Content.Shared._Starlight.Genetics.GeneticTraits.Prototypes;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Genetics.GeneticTraits.Systems;

public sealed partial class TraitPeriodicItemSpawnerSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private MetaDataSystem _metaDataSystem = default!;
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TraitPeriodicItemSpawnerComponent, UpdateTraitComponentsEvent>(OnUpdateTraitComponents);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<TraitPeriodicItemSpawnerComponent>();
        while (query.MoveNext(out var uid, out var traitPeriodicItemSpawnerComponent))
        {
            if (_metaDataSystem.EntityPaused(uid)) continue;
            if (_mobStateSystem.IsIncapacitated(uid)) continue;
            foreach (var kv in traitPeriodicItemSpawnerComponent.WhenNextSpawns)
            {
                var protoid = kv.Key;
                var nextSpawn = kv.Value;
                if (_gameTiming.CurTime < nextSpawn) continue;
                var collection = traitPeriodicItemSpawnerComponent.SpawnCollectionInstances[protoid];

                foreach (var item in collection.Items)
                    for (var i = 0; i < item.Amount; i++)
                        _entityManager.PredictedSpawnNextToOrDrop(item.Id, uid);

                traitPeriodicItemSpawnerComponent.WhenNextSpawns[protoid] += collection.Cooldown;
            }
        }
    }

    private void OnUpdateTraitComponents(Entity<TraitPeriodicItemSpawnerComponent> ent, ref UpdateTraitComponentsEvent args)
    {
        var found = false;

        foreach (var kv in args.Traits.Traits)
        {
            if (!_prototypeManager.TryIndex<GeneticTraitPrototype>(kv.Key,
                    out var traitPeriodicItemSpawnerPrototype)) continue;
            foreach (var part in traitPeriodicItemSpawnerPrototype.Parts)
            {
                if (part is not TraitPeriodicItemSpawnerPart) continue;
                var traitPeriodicItemSpawnerPart = (TraitPeriodicItemSpawnerPart)part;
                found = true;
                _entityManager.EnsureComponent<TraitPeriodicItemSpawnerComponent>(ent.Owner, out var traitPeriodicItemSpawnerComponent);
                foreach (var collection in traitPeriodicItemSpawnerPart.Collections)
                {
                    if (!_prototypeManager.TryIndex<SpawnCollectionPrototype>(collection, out var collectionProto))
                        continue;
                    var instance = new SpawnCollectionInstance(collectionProto, (int)Math.Floor(kv.Value.Float()));
                    if (traitPeriodicItemSpawnerComponent.SpawnCollectionInstances.TryGetValue(collection, out var collectionInstance))
                        instance += collectionInstance;
                    traitPeriodicItemSpawnerComponent.SpawnCollectionInstances[collection] = instance;
                    traitPeriodicItemSpawnerComponent.WhenNextSpawns[collection] = _gameTiming.CurTime + instance.Cooldown;
                }
            }
        }

        if (!found)
            _entityManager.RemoveComponent<TraitPeriodicItemSpawnerComponent>(ent.Owner);
    }
}
