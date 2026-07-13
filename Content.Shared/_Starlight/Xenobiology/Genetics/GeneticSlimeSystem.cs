using Content.Shared._Starlight.Genetics;
using Content.Shared._Starlight.Genetics.Components;
using Content.Shared._Starlight.Genetics.Systems;
using Content.Shared._Starlight.Weapons.Melee.Events;
using Content.Shared.Coordinates;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Xenobiology.Genetics;

public sealed class GeneticSlimeSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly GenesSystem _genesSystem = default!;
    [Dependency] private readonly HungerSystem _hungerSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticSlimeComponent, AfterMeleeHitEvent>(OnAfterMeleeHit);
    }

    private void OnAfterMeleeHit(Entity<GeneticSlimeComponent> entity, ref AfterMeleeHitEvent args)
    {
        if (!_entityManager.TryGetComponent<HungerComponent>(entity.Owner, out var hungerComponent)) return;
        if (!_entityManager.TryGetComponent<GenesComponent>(entity.Owner, out var genesComponent)) return;

        // Yes this means slimes can get nutrition from eating walls
        // And that sounds hilarious so I'm keeping it
        foreach (var target in args.HitEntities)
            _hungerSystem.ModifyHunger(entity.Owner, entity.Comp.BiteNutritionGain);

        if (_hungerSystem.GetHungerThreshold(hungerComponent) > HungerThreshold.Okay)
        {
            var newNutrition = _hungerSystem.GetHunger(hungerComponent) / entity.Comp.SplitAmount;
            for (int i = 0; i < entity.Comp.SplitAmount; i++)
            {
                var protoName = _entityManager.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID;
                var split = _entityManager.PredictedSpawnAtPosition(protoName, entity.Owner.ToCoordinates());
                _hungerSystem.SetHunger(split, newNutrition);
                var newGenesComponent = _entityManager.EnsureComponent<GenesComponent>(split);

                newGenesComponent.Genes = new List<Gene>(genesComponent.Genes); // copy
                if (_robustRandom.NextDouble() < .25f)
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
                        newGenesComponent.Genes.RemoveAt(indexToRemove);
                    }
                    if (shouldAdd)
                    {
                        var newGene = _genesSystem.GenerateGene((split, newGenesComponent));
                        newGenesComponent.Genes.Add(newGene);
                    }
                }
            }
            _entityManager.PredictedQueueDeleteEntity(entity.Owner);
        }
    }
}
