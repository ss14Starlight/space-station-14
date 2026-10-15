using Content.Shared._Starlight.Genetics.Components;
using Content.Shared._Starlight.Genetics.Genes.Components;
using Content.Shared._Starlight.Genetics.Genes.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Containers;

// ReSharper disable UseCollectionExpression

namespace Content.Shared._Starlight.Genetics.Systems;

public sealed partial class GeneSamplerSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private SharedInteractionSystem _interactionSystem = null!;
    [Dependency] private SharedContainerSystem _container = null!;
    [Dependency] private IndividualGeneSystem _iGene = null!;
    [Dependency] private GenesSystem _genes = null!;

    [SubscribeLocalEvent]
    private void OnComponentInit(Entity<GeneSamplerComponent> ent, ref ComponentInit args) =>
        ent.Comp.GeneContainer = _container.EnsureContainer<Container>(ent, GeneSamplerComponent.GeneContainerId);

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<GeneSamplerComponent> ent, ref MapInitEvent args) => StateChanged(ent);

    [SubscribeLocalEvent]
    private void OnAfterInteract(Entity<GeneSamplerComponent> entity, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue) return;
        var canReach = _interactionSystem.InRangeUnobstructed(args.User, args.Target.Value);
        if (!canReach) return;
        if (!TryComp<GenesComponent>(args.Target.Value, out var genesComponent)) return;

        if (entity.Comp.IsCurrentlySampling)
            FillGenes(entity, genesComponent.Genes);
        else
        {
            foreach (var gene in entity.Comp.Genes)
                _genes.InsertGene((args.Target.Value, genesComponent), gene);
            ClearGenes(entity);
        }
        args.Handled = true;
    }

    /// <summary>
    /// Sets the sampler's genes and prepares it for injecting.
    /// Prefer this to directly updating the component.
    /// Safe to be called repeatedly.
    /// </summary>
    /// <param name="entity">The gene sampler.</param>
    /// <param name="genes">The genes it will receive.</param>
    public void FillGenes(Entity<GeneSamplerComponent> entity, HashSet<EntityUid> genes)
    {
        entity.Comp.Genes.Clear();
        foreach (var gene in genes)
        {
            if (!TryComp<IndividualGeneComponent>(gene, out var comp)) continue;
            var clone = _iGene.Clone((gene, comp));
            entity.Comp.Genes.Add(clone);
            _container.Insert(clone.Owner, entity.Comp.GeneContainer);
        }
        entity.Comp.IsCurrentlySampling = false;
        StateChanged(entity);
    }

    /// <summary>
    /// Clears the sampler's genes and prepares it for sampling.
    /// Prefer this to directly updating the component.
    /// Safe to be called repeatedly.
    /// </summary>
    /// <param name="entity">The gene sampler.</param>
    public void ClearGenes(Entity<GeneSamplerComponent> entity)
    {
        entity.Comp.Genes.Clear();
        entity.Comp.IsCurrentlySampling = true;
        StateChanged(entity);
    }

    private void StateChanged(Entity<GeneSamplerComponent> entity)
    {
        if (TryComp(entity.Owner, out AppearanceComponent? appearance))
            _appearance.SetData(entity.Owner, GeneSamplerVisuals.Signal, entity.Comp.IsCurrentlySampling, appearance);
        Dirty(entity);
    }
}
