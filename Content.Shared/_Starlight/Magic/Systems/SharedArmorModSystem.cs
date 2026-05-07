using Content.Shared._Starlight.Magic.Components;
using Content.Shared.Armor;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Stunnable;

using Content.Shared.StatusEffectNew;

namespace Content.Shared._Starlight.Magic.Systems;

// Obviously a lot of the code in this class is highly derivative of SharedArmorSystem.
// This is because the 'correct' way to refactor would be to make SharedArmorSystem derivative of this class!

// Are YOU brave enough to turn that ship around? I'm not.

/// <summary>
///     This handles logic relating to <see cref="ArmorModComponent" /> and <see cref="ArmorModStatusEffectComponent" />
/// </summary>
public sealed class SharedArmorModSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorModStatusEffectComponent, StatusEffectRemovedEvent>(ArmorModStatusEffectRemoved);
        SubscribeLocalEvent<ArmorModStatusEffectComponent, StatusEffectAppliedEvent>(ArmorModStatusEffectApplied);

        // SubscribeLocalEvent<ArmorModComponent, CoefficientQueryEvent>(OnCoefficientQuery);
        SubscribeLocalEvent<ArmorModComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<ArmorModComponent, StaminaModifyEvent>(OnStaminaDamageModify);
        // SubscribeLocalEvent<ArmorModComponent, DamageModifyEvent>(OnBorgDamageModify);

        SubscribeLocalEvent<ArmorModComponent, KnockDownAttemptEvent>(OnKnockdownAttempt);
    }

    // --- code for dealing with applying and removing StatusEffects --- //

    private void ArmorModStatusEffectApplied(EntityUid ent, ArmorModStatusEffectComponent effect, ref StatusEffectAppliedEvent args)
    {
        // TODO: spawn ArmorModComponent and attach to entity
    }

    private void ArmorModStatusEffectRemoved(EntityUid ent, ArmorModStatusEffectComponent effect, ref StatusEffectRemovedEvent args)
    {
        // TODO: remove ArmorModComponent from entity
    }

    // --- code shamelessly cribbed from SharedArmorSystem.cs --- //

    private void OnKnockdownAttempt(EntityUid uid, ArmorModComponent component, KnockDownAttemptEvent args)
    {
        if (component.IgnoreKnockdown && !args.Voluntary)
            args.Cancelled = true;
    }

    /*
    private void OnCoefficientQuery(Entity<ArmorModComponent> ent, ref CoefficientQueryEvent args)
    {
        foreach (var armorCoefficient in ent.Comp.Modifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[armorCoefficient.Key] = args.Args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient) ? coefficient * armorCoefficient.Value : armorCoefficient.Value;
        }
    }
    */
    private void OnDamageModify(EntityUid uid, ArmorModComponent component, DamageModifyEvent args)
    {
        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, component.Modifiers, args.ArmorPenetration, args.CanHeal);
    }

    private void OnStaminaDamageModify(EntityUid uid, ArmorModComponent component, StaminaModifyEvent args)
    {
        if (args.Damage < 0)
            return;

        if (args.Modifier > component.StaminaDamageModifier)
            args.Modifier = component.StaminaDamageModifier;
    }

    /*private void OnBorgDamageModify(EntityUid uid, ArmorModComponent component, ref DamageModifyEvent args)
    {
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers, args.Args.ArmorPenetration, args.Args.CanHeal);
    }*/
}
