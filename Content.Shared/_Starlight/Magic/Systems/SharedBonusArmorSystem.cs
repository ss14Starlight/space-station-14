using Content.Shared._Starlight.Magic.Components;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Explosion;

using Content.Shared.StatusEffectNew;

namespace Content.Shared._Starlight.Magic.Systems;

/// <summary>
///     This handles logic relating to <see cref="BonusArmorComponent" /> and <see cref="BonusArmorStatusEffectComponent" />.
///
///     Not used in the handling of actual wearable armor items.
/// </summary>
public sealed class SharedBonusArmorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BonusArmorStatusEffectComponent, StatusEffectAppliedEvent>(BonusArmorStatusEffectApplied);
        SubscribeLocalEvent<BonusArmorStatusEffectComponent, StatusEffectRemovedEvent>(BonusArmorStatusEffectRemoved);

        SubscribeLocalEvent<BonusArmorComponent, CoefficientQueryEvent>(OnCoefficientQuery);

        SubscribeLocalEvent<BonusArmorComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<BonusArmorComponent, StaminaModifyEvent>(OnStaminaDamageModify);
        SubscribeLocalEvent<BonusArmorComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance);

        // Unlike SharedArmorSystem, there's no special borg handler,
        // as that merely resolves borg anatomy versus human anatomy.
        // BonusArmors should work fine on borgs regardless.

        SubscribeLocalEvent<BonusArmorComponent, KnockDownAttemptEvent>(OnKnockdownAttempt);
    }

    private void OnGetExplosionResistance(EntityUid uid, BonusArmorComponent component, ref GetExplosionResistanceEvent args)
    {
        foreach (var modifier in component.modifiers)
            args.DamageCoefficient *= modifier.Value.ExplosionResistance;
    }

    private void BonusArmorStatusEffectApplied(EntityUid ent, BonusArmorStatusEffectComponent effect, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp(args.Target, out BonusArmorComponent? component)) // This may be idiomatic in C#, but Dennis Ritchie is rolling in his grave over this violation of scoping rules. Why does if() have leaky scope in its expression, when for() and while() don't?!
            component = AddComp<BonusArmorComponent>(args.Target);

        if (!component.modifiers.ContainsKey(ent) || effect.OverwriteOnRefresh)
        {
            if(component.modifiers.ContainsKey(ent))
            {
                // refresh the buff only if OverwriteOnRefresh applies and the key was found:
                component.modifiers.Remove(ent);
            }

            component.modifiers[ent] = new BonusArmor()
            {
                ExplosionResistance = effect.ExplosionResistance,
                IgnoreKnockdown = effect.IgnoreKnockdown,
                StaminaDamageModifier = effect.StaminaDamageModifier,
                Modifiers = new DamageModifierSet(effect.Modifiers)
            };
        }
    }

    private void BonusArmorStatusEffectRemoved(EntityUid ent, BonusArmorStatusEffectComponent effect, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp(args.Target, out BonusArmorComponent? component))
            return;

        if (component.modifiers.ContainsKey(ent))
            component.modifiers.Remove(ent);

        if (component.modifiers.Count == 0)
            RemComp<BonusArmorComponent>(args.Target);
    }

    private void OnKnockdownAttempt(EntityUid uid, BonusArmorComponent component, KnockDownAttemptEvent args)
    {
        if (!args.Voluntary)
        {
            foreach (var modifier in component.modifiers)
            {
                if (modifier.Value.IgnoreKnockdown)
                {
                    args.Cancelled = true;
                    return;
                }
            }
        }
    }

    private void OnCoefficientQuery(Entity<BonusArmorComponent> ent, ref CoefficientQueryEvent args)
    {
        foreach (var modifier in ent.Comp.modifiers)
            foreach (var armorCoefficient in modifier.Value.Modifiers.Coefficients)
                args.DamageModifiers.Coefficients[armorCoefficient.Key] = args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient) ? coefficient * armorCoefficient.Value : armorCoefficient.Value;
    }

    private void OnDamageModify(EntityUid uid, BonusArmorComponent component, DamageModifyEvent args)
    {
        foreach (var modifier in component.modifiers)
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifier.Value.Modifiers, args.ArmorPenetration, args.CanHeal);
    }

    private void OnStaminaDamageModify(EntityUid uid, BonusArmorComponent component, StaminaModifyEvent args)
    {
        if (args.Damage < 0)
            return;

        foreach (var modifier in component.modifiers)
            if (args.Modifier > modifier.Value.StaminaDamageModifier)
                args.Modifier = modifier.Value.StaminaDamageModifier;
    }
}
