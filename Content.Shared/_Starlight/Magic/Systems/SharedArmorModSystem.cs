using Content.Shared._Starlight.Magic.Components;
using Content.Shared.Armor;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Stunnable;

using Content.Shared.StatusEffectNew;

namespace Content.Shared._Starlight.Magic.Systems;

/// <summary>
///     This handles logic relating to <see cref="ArmorModComponent" /> and <see cref="ArmorModStatusEffectComponent" />.
///
///     Not used in the handling of actual wearable armor items.
/// </summary>
public sealed class SharedArmorModSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorModStatusEffectComponent, StatusEffectAppliedEvent>(ArmorModStatusEffectApplied);
        SubscribeLocalEvent<ArmorModStatusEffectComponent, StatusEffectRemovedEvent>(ArmorModStatusEffectRemoved);

        SubscribeLocalEvent<ArmorModComponent, CoefficientQueryEvent>(OnCoefficientQuery);

        SubscribeLocalEvent<ArmorModComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<ArmorModComponent, StaminaModifyEvent>(OnStaminaDamageModify);

        // unimplemented since this just handles the special case of borg gear being organized differently from clothing (not applicable to these intrinsic mods):
        // SubscribeLocalEvent<ArmorModComponent, DamageModifyEvent>(OnBorgDamageModify);

        SubscribeLocalEvent<ArmorModComponent, KnockDownAttemptEvent>(OnKnockdownAttempt);
    }

    private void ArmorModStatusEffectApplied(EntityUid ent, ArmorModStatusEffectComponent effect, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp(args.Target, out ArmorModComponent? component)) // this syntax is a crime against PLT btw
            component = AddComp<ArmorModComponent>(args.Target);

        if (!component.modifiers.ContainsKey(ent))
            component.modifiers[ent] = new ArmorMod(effect.Modifiers, effect.IgnoreKnockdown, effect.StaminaDamageModifier);
    }

    private void ArmorModStatusEffectRemoved(EntityUid ent, ArmorModStatusEffectComponent effect, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp(args.Target, out ArmorModComponent? component))
            return;

        if (component.modifiers.ContainsKey(ent))
            component.modifiers.Remove(ent);

        if (component.modifiers.Count == 0)
            RemComp<ArmorModComponent>(args.Target);
    }

    private void OnKnockdownAttempt(EntityUid uid, ArmorModComponent component, KnockDownAttemptEvent args)
    {
        if (!args.Voluntary) {
            foreach (var modifier in component.modifiers) {
                if (modifier.Value.IgnoreKnockdown ) {
                    args.Cancelled = true;
                    return;
                }
            }
        }
    }

    private void OnCoefficientQuery(Entity<ArmorModComponent> ent, ref CoefficientQueryEvent args)
    {
        if (!TryComp<ArmorModComponent>(ent, out var component))
            return;

        foreach (var modifier in component.modifiers)
            foreach (var armorCoefficient in modifier.Value.Modifiers.Coefficients)
                args.DamageModifiers.Coefficients[armorCoefficient.Key] = args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient) ? coefficient * armorCoefficient.Value : armorCoefficient.Value;
    }

    private void OnDamageModify(EntityUid uid, ArmorModComponent component, DamageModifyEvent args)
    {
        foreach (var modifier in component.modifiers)
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifier.Value.Modifiers, args.ArmorPenetration, args.CanHeal);
    }

    private void OnStaminaDamageModify(EntityUid uid, ArmorModComponent component, StaminaModifyEvent args)
    {
        if (args.Damage < 0)
            return;

        foreach (var modifier in component.modifiers)
            if (args.Modifier > modifier.Value.StaminaDamageModifier)
                args.Modifier = modifier.Value.StaminaDamageModifier;
    }

    /*private void OnBorgDamageModify(EntityUid uid, ArmorModComponent component, ref DamageModifyEvent args)
    {
        foreach (var modifier in component.modifiers)
            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, modifier.Value.Modifiers, args.Args.ArmorPenetration, args.Args.CanHeal);
    }*/
}
