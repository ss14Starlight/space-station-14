using Content.Shared._Starlight.Magic.Components;

using Content.Shared.StatusEffectNew;

namespace Content.Shared._Starlight.Magic.Systems;

/// <summary>
///     This handles logic relating to <see cref="BonusDamageComponent" /> and <see cref="BonusDamageStatusEffectComponent" />.
///
///     Not used in the handling of actual wearable armor items.
/// </summary>
public sealed class SharedBonusDamageSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BonusDamageStatusEffectComponent, StatusEffectAppliedEvent>(BonusDamageStatusEffectApplied);
        SubscribeLocalEvent<BonusDamageStatusEffectComponent, StatusEffectRemovedEvent>(BonusDamageStatusEffectRemoved);
    }

    private void BonusDamageStatusEffectApplied(EntityUid ent, BonusDamageStatusEffectComponent effect, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp(args.Target, out BonusDamageComponent? component))
            component = AddComp<BonusDamageComponent>(args.Target);

        if (!component.modifiers.ContainsKey(ent) || effect.OverwriteOnRefresh) {
            if(component.modifiers.ContainsKey(ent))
            {
                // refresh the buff only if OverwriteOnRefresh applies and the key was found:
                component.modifiers.Remove(ent);
            }

            // must be careful to copy these by value since future work may make them change dynamically:
            component.modifiers[ent] = new() {
                DamageTypes = new Dictionary<string, float>(effect.DamageTypes),
                AffectsUnarmed = effect.AffectsUnarmed,
                AffectsMeleeWeapons = effect.AffectsMeleeWeapons,
                AffectsRangedWeapons = effect.AffectsRangedWeapons
            };
        }
    }

    private void BonusDamageStatusEffectRemoved(EntityUid ent, BonusDamageStatusEffectComponent effect, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp(args.Target, out BonusDamageComponent? component))
            return;

        if (component.modifiers.ContainsKey(ent))
            component.modifiers.Remove(ent);

        if (component.modifiers.Count == 0)
            RemComp<BonusDamageComponent>(args.Target);
    }
}
