using System.Linq;
using Content.Shared._Starlight.Genetics.Genes.Components;
using Content.Shared._Starlight.Genetics.GeneticTraits;
using Content.Shared._Starlight.Genetics.GeneticTraits.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics.Genes.Systems;

public sealed class GenesSystem : EntitySystem
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public TraitDict GetTraitsFromEnumerable(IEnumerable<EntityUid> genes) => TraitDict.Combine(genes.Select(g =>
    {
        if (!TryComp<IndividualGeneComponent>(g, out var comp)) return new TraitDict();
        return comp.Traits;
    }));

    public TraitDict GetTraits(params EntityUid[] genes) => GetTraitsFromEnumerable(genes);

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
            foreach (var part in proto.Parts) part.GeneticTraitSetup(_entityManager, entity.Owner);
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
