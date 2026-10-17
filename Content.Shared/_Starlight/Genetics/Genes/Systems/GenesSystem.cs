using System.Linq;
using Content.Shared._Starlight.Genetics.Genes.Components;
using Content.Shared._Starlight.Genetics.GeneticTraits;
using Content.Shared._Starlight.Genetics.GeneticTraits.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics.Genes.Systems;

public sealed partial class GenesSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = null!;
    [Dependency] private SharedContainerSystem _container = null!;
    [Dependency] private INetManager _net = null!;

    [SubscribeLocalEvent]
    private void OnComponentInit(Entity<GenesComponent> ent, ref ComponentInit args) =>
        ent.Comp.GeneContainer = _container.EnsureContainer<Container>(ent, GenesComponent.GeneContainerId);

    public TraitDict GetTraitsFromEnumerable(IEnumerable<EntityUid> genes) => TraitDict.Combine(genes.Select(g =>
        !TryComp<IndividualGeneComponent>(g, out var comp) ? new TraitDict() : comp.Traits));

    public TraitDict GetTraits(params EntityUid[] genes) => GetTraitsFromEnumerable(genes);

    public void RemoveGene(Entity<GenesComponent> ent, EntityUid gene)
    {
        ent.Comp.Genes.Remove(gene);
        _container.Remove(gene, ent.Comp.GeneContainer);
    }

    public void InsertGene(Entity<GenesComponent> ent, EntityUid gene)
    {
        ent.Comp.Genes.Add(gene);
        _container.Insert(gene, ent.Comp.GeneContainer);
    }

    public void UpdateTraits(Entity<GenesComponent> entity)
    {
        var newTraits = GetTraitsFromEnumerable(entity.Comp.Genes);
        var traitsToSend = new TraitDict();
        foreach (var trait in newTraits.Traits)
        {
            if (!_prototypeManager.TryIndex<GeneticTraitPrototype>(trait.Key.Id, out var proto)) continue;
            if (traitsToSend.Traits.ContainsKey(trait.Key.Id))
                traitsToSend.Traits[trait.Key.Id] += trait.Value;
            else
                traitsToSend.Traits.Add(trait.Key.Id, trait.Value);
            foreach (var part in proto.Parts) part.GeneticTraitSetup(EntityManager, entity.Owner);
        }

        foreach (var tk in traitsToSend.Traits.Keys.ToList())
        {
            if (!_prototypeManager.TryIndex<GeneticTraitPrototype>(tk, out var proto)) continue;
            if (!entity.Comp.Classes.Intersect(proto.Classes).Any() || proto.Threshold < traitsToSend.Traits[tk])
                traitsToSend.Traits.Remove(tk);
        }

        var ev = new UpdateTraitComponentsEvent(traitsToSend);
        RaiseLocalEvent(entity.Owner, ev);
    }
}
