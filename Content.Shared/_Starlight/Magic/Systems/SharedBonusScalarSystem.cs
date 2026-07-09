using Content.Shared._Starlight.Magic.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Starlight.Magic.Systems;

/// <summary>
///     This handles logic relating to <see cref="BonusScalarComponent" /> and <see cref="BonusScalarStatusEffectComponent" />.
///
///     Not used in the handling of actual wearable armor items.
/// </summary>
public sealed class SharedBonusScalarSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BonusScalarStatusEffectComponent, StatusEffectAppliedEvent>(BonusScalarStatusEffectApplied);
        SubscribeLocalEvent<BonusScalarStatusEffectComponent, StatusEffectRemovedEvent>(BonusScalarStatusEffectRemoved);
        SubscribeLocalEvent<BonusScalarComponent, GetMeleeDamageEvent>(OnGetBonusMeleeDamage);
        SubscribeLocalEvent<BonusScalarComponent, GetMeleeAttackRateEvent>(OnGetBonusMeleeAttackRate);
    }

    private void OnGetBonusMeleeDamage(EntityUid uid, BonusScalarComponent component, ref GetMeleeDamageEvent args)
    {
        if (args.User == args.Weapon) {
            args.Damage *= component.unarmedDamage;
        } else {
            args.Damage *= component.meleeWeaponDamage;
        }
    }

    private void OnGetBonusMeleeAttackRate(EntityUid uid, BonusScalarComponent component, ref GetMeleeAttackRateEvent args)
    {
        if (args.User == args.Weapon) {
            args.Multipliers *= component.unarmedAttackRate;
        } else {
            args.Multipliers *= component.meleeWeaponAttackRate;
        }
    }

    private void Recalculate(EntityUid ent, BonusScalarComponent component)
    {
        component.unarmedAttackRate = 1.0f;
        component.unarmedDamage = 1.0f;
        component.meleeWeaponAttackRate = 1.0f;
        component.meleeWeaponDamage = 1.0f;
        /* // TODO
        component.rangedWeaponDamage = 1.0f;
        component.rangedWeaponAttackRate = 1.0f; */
        component.doAfterDelay = 1.0f;
        foreach (var modifier in component.modifiers)
        {
            if(modifier.Value.unarmedAttackRate != 0) component.unarmedAttackRate *= modifier.Value.unarmedAttackRate;
            if(modifier.Value.unarmedDamage != 0) component.unarmedDamage *= modifier.Value.unarmedDamage;
            if(modifier.Value.meleeWeaponAttackRate != 0) component.meleeWeaponAttackRate *= modifier.Value.meleeWeaponAttackRate;
            if(modifier.Value.meleeWeaponDamage != 0) component.meleeWeaponDamage *= modifier.Value.meleeWeaponDamage;
            /*
            // TODO
            if(modifier.Value.rangedWeaponDamage != 0) component.rangedWeaponDamage *= modifier.Value.rangedWeaponDamage;
            if(modifier.Value.rangedWeaponAttackRate != 0) component.rangedWeaponAttackRate *= modifier.Value.rangedWeaponAttackRate;
            */
            if(modifier.Value.doAfterDelay != 0) component.doAfterDelay *= modifier.Value.doAfterDelay;
        }
        Dirty(ent, component);
    }

    private void BonusScalarStatusEffectApplied(EntityUid ent, BonusScalarStatusEffectComponent effect, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp(args.Target, out BonusScalarComponent? component))
            component = AddComp<BonusScalarComponent>(args.Target);

        if (!component.modifiers.ContainsKey(ent) || effect.OverwriteOnRefresh) {
            if (component.modifiers.ContainsKey(ent))
            {
                // refresh the buff only if OverwriteOnRefresh applies and the key was found:
                component.modifiers.Remove(ent);
            }

            // this /should/ copy, since BonusScalarCoefficients is a struct:
            component.modifiers[ent] = effect.coefficients;
        }

        Recalculate(ent, component);
    }

    private void BonusScalarStatusEffectRemoved(EntityUid ent, BonusScalarStatusEffectComponent effect, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp(args.Target, out BonusScalarComponent? component))
            return;

        if (component.modifiers.ContainsKey(ent))
            component.modifiers.Remove(ent);

        if (component.modifiers.Count == 0)
            RemComp<BonusScalarComponent>(args.Target);
        else
            Recalculate(ent, component);
    }
}
