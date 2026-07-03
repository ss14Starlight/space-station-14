using Content.Shared._Starlight.Genetics.Components;
using Content.Shared._Starlight.Genetics.Systems;

namespace Content.Shared._Starlight.Xenobiology.Genetics;

public sealed class GeneticSlimeSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly GenesSystem _genesSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticSlimeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<GeneticSlimeComponent> entity, ref MapInitEvent args)
    {
        if (_entityManager.TryGetComponent<GenesComponent>(entity, out var genesComponent))
        {
            for (var i = 0; i < 5; i++)
            {
                var newGene = _genesSystem.GenerateGene((entity.Owner, genesComponent));
                genesComponent.Genes.Add(newGene);
            }
            _genesSystem.UpdateTraits((entity.Owner, genesComponent));
        }
    }
}
