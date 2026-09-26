using System.Linq;
using Content.Shared._Starlight.Genetics.Genes.Systems;
using Content.Shared._Starlight.Weapons.Melee.Events;
using Content.Shared.Coordinates;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using GenesComponent = Content.Shared._Starlight.Genetics.Genes.Components.GenesComponent;

namespace Content.Shared._Starlight.Xenobiology.Genetics;

public sealed partial class GeneticSlimeSystem : EntitySystem
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private GenesSystem _genesSystem = default!;
    [Dependency] private IndividualGeneSystem _individualGeneSystem = default!;
    [Dependency] private HungerSystem _hungerSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticSlimeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GeneticSlimeComponent, AfterMeleeHitEvent>(OnAfterMeleeHit);
    }

    private void OnMapInit(Entity<GeneticSlimeComponent> entity, ref MapInitEvent args)
    {
        if (!entity.Comp.ShouldAddStarters) return;
        if (!_entityManager.TryGetComponent<GenesComponent>(entity.Owner, out var genesComponent)) return;
        foreach (var id in entity.Comp.StartingGeneIDs)
        {
            var gene = _individualGeneSystem.GeneFromSample(id);
            genesComponent.Genes.Add(gene);
        }
        _genesSystem.UpdateTraits((entity, genesComponent));
    }

    private void OnAfterMeleeHit(Entity<GeneticSlimeComponent> entity, ref AfterMeleeHitEvent args)
    {
        // Yes this means slimes can get nutrition from eating walls
        // And that sounds hilarious so I'm keeping it
        foreach (var target in args.HitEntities)
            _hungerSystem.ModifyHunger(entity.Owner, entity.Comp.BiteNutritionGain);

        if (!_entityManager.TryGetComponent<HungerComponent>(entity.Owner, out var hungerComponent)) return;
        if (_hungerSystem.GetHungerThreshold(hungerComponent) > HungerThreshold.Okay)
        {
            Split(entity);
        }
    }

    private void Split(Entity<GeneticSlimeComponent> entity)
    {
        if (!_entityManager.TryGetComponent<HungerComponent>(entity.Owner, out var hungerComponent)) return;
        if (!_entityManager.TryGetComponent<GenesComponent>(entity.Owner, out var genesComponent)) return;
        var newNutrition = _hungerSystem.GetHunger(hungerComponent) / entity.Comp.SplitAmount;
        for (int i = 0; i < entity.Comp.SplitAmount; i++)
        {
            var protoName = entity.Comp.SplitEntity;
            var split = _entityManager.PredictedSpawnAtPosition(protoName, entity.Owner.ToCoordinates());
            _hungerSystem.SetHunger(split, newNutrition);
            var newGeneticSlimeComponent = _entityManager.EnsureComponent<GeneticSlimeComponent>(split);
            newGeneticSlimeComponent.ShouldAddStarters = false; // Just in case an attempt to add the starter genes happens after Split
            var newGenesComponent = _entityManager.EnsureComponent<GenesComponent>(split);

            newGenesComponent.Genes = new HashSet<EntityUid>(genesComponent.Genes); // shallow copy

            if (_robustRandom.NextDouble() < 0.25d)
            {
                bool shouldAdd = false;
                bool shouldRemove = false;
                var chance = _robustRandom.NextDouble();
                if (chance < 3.0/5.0)
                    shouldAdd = true; // Add a gene
                else if (chance < 4.0/5.0)
                    shouldRemove = true; // Remove a gene
                else
                {
                    shouldAdd = true; // Swap a gene
                    shouldRemove = true;
                }

                if (newGenesComponent.Genes.Count == 0)
                    shouldRemove = false;
                if (shouldRemove)
                {
                    var indexToRemove = _robustRandom.Next(0, newGenesComponent.Genes.Count - 1);
                    var geneToRemove = newGenesComponent.Genes.ElementAt(indexToRemove);
                    newGenesComponent.Genes.Remove(geneToRemove);
                }
                if (shouldAdd)
                {
                    var newGene = _individualGeneSystem.GenerateGene((split, newGenesComponent));
                    newGenesComponent.Genes.Add(newGene);
                }
            }
            _genesSystem.UpdateTraits((split, newGenesComponent));
        }
        _entityManager.PredictedQueueDeleteEntity(entity.Owner);
    }
}
