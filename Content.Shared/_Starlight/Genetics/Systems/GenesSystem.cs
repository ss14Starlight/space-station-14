using System.Linq;
using System.Text;
using Content.Shared._Starlight.Genetics.Components;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Genetics.Systems;

public sealed class GenesSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffectsSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public TraitDict GetTraitsFromEnumerable(IEnumerable<EntityUid> genes) => TraitDict.Combine(genes.Select(g =>
    {
        if (!TryComp<IndividualGeneComponent>(g, out var comp)) return new TraitDict();
        return comp.Traits;
    }));

    public TraitDict GetTraits(params EntityUid[] genes) => GetTraitsFromEnumerable(genes);

    public void UpdateTraits(Entity<GenesComponent> entity)
    {
        var newTraits = GetTraitsFromEnumerable(entity.Comp.Genes).Traits;

        Dictionary<ProtoId<OnceTraitPrototype>, FixedPoint2> newOnceTraits = new();
        Dictionary<ProtoId<OnSolutionChangedTraitPrototype>, FixedPoint2> newOnSolutionChangedTraits = new();
        Dictionary<ProtoId<PassiveTraitPrototype>, FixedPoint2> newPassiveTraits = new();
        Dictionary<ProtoId<PassiveTraitPrototype>, TimeSpan> newPassiveTraitsCooldowns = new();
        foreach (var t in newTraits)
        {
            if (_prototypeManager.TryIndex<OnceTraitPrototype>(t.Key, out var proto1) &&
                (!proto1.Threshold.HasValue || t.Value >= proto1.Threshold.Value) && entity.Comp.Classes.Intersect(proto1.Classes).Any())
                newOnceTraits.Add(proto1, t.Value);
            else if (_prototypeManager.TryIndex<OnSolutionChangedTraitPrototype>(t.Key, out var proto2) &&
                     (!proto2.Threshold.HasValue || t.Value >= proto2.Threshold.Value) && entity.Comp.Classes.Intersect(proto2.Classes).Any())
                newOnSolutionChangedTraits.Add(proto2, t.Value);
            else if (_prototypeManager.TryIndex<PassiveTraitPrototype>(t.Key, out var proto3) &&
                     (!proto3.Threshold.HasValue || t.Value >= proto3.Threshold.Value) && entity.Comp.Classes.Intersect(proto3.Classes).Any())
            {
                newPassiveTraits.Add(proto3, t.Value);
                newPassiveTraitsCooldowns.Add(proto3, proto3.Cooldown + _gameTiming.CurTime);
            }

        }

        if (_entityManager.TryGetComponent<Components.OnceTraitsComponent>(entity, out var onceTraits))
        {
            /*
             * Okay, this is going to be ugly.
             * The once traits need to be updated in the following way:
             * 1. If a trait exists in the old list but not in the new list, call onRemoved
             * 2. If a trait does not exist in the old list but does exist in the new list, call onAdded
             * 3. If a trait exists in both lists AND the trait has an onUpdated, call onUpdated
             * 4. If a trait exists in both lists AND the trait does not have an onUpdated, call onRemoved and then onAdded
             * wait, I can handle all of this with sets, what am I doing?
             * 1 is O - N
             * 2 is N - O
             * 3 and 4 are both O /\ N
             */

            foreach (var t in onceTraits.Traits.Keys.Except(newOnceTraits.Keys))
            {
                var trait = _prototypeManager.Index(t);
                _entityEffectsSystem.TryApplyEffect(entity.Owner, trait.OnRemovedEffect.Effect,
                    ((onceTraits.Traits[t] * trait.OnRemovedEffect.ScalingFactor) + trait.OnRemovedEffect.ScalingOffset).Float());
            }
            foreach (var t in newOnceTraits.Keys.Except(onceTraits.Traits.Keys))
            {
                var trait = _prototypeManager.Index(t);
                _entityEffectsSystem.TryApplyEffect(entity.Owner, trait.OnAddedEffect.Effect,
                    ((newOnceTraits[t] * trait.OnAddedEffect.ScalingFactor) + trait.OnAddedEffect.ScalingOffset).Float());
            }
            foreach (var t in onceTraits.Traits.Keys.Intersect(newOnceTraits.Keys))
            {
                var trait = _prototypeManager.Index(t);
                if (trait.OnUpdatedEffect is null)
                {
                    _entityEffectsSystem.TryApplyEffect(entity.Owner, trait.OnRemovedEffect.Effect,
                        ((onceTraits.Traits[t] * trait.OnRemovedEffect.ScalingFactor) + trait.OnRemovedEffect.ScalingOffset).Float());
                    _entityEffectsSystem.TryApplyEffect(entity.Owner, trait.OnAddedEffect.Effect,
                        ((newOnceTraits[t] * trait.OnAddedEffect.ScalingFactor) + trait.OnAddedEffect.ScalingOffset).Float());
                }
                else
                {
                    _entityEffectsSystem.TryApplyEffect(entity.Owner, trait.OnUpdatedEffect.Effect,
                        ((newOnceTraits[t] * trait.OnUpdatedEffect.ScalingFactor) + trait.OnUpdatedEffect.ScalingOffset).Float());
                }
            }

            onceTraits.Traits = newOnceTraits;
        }

        if (_entityManager.TryGetComponent<Components.OnSolutionChangedTraitsComponent>(entity, out var onSolutionChangedTraits))
            onSolutionChangedTraits.Traits = newOnSolutionChangedTraits;

        if (_entityManager.TryGetComponent<Components.PassiveTraitsComponent>(entity, out var passiveTraits))
        {
            passiveTraits.Traits = newPassiveTraits;
            passiveTraits.Cooldowns = newPassiveTraitsCooldowns;
        }
    }
}
