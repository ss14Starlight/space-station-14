using Content.Shared._Starlight.Magic.Components;

using Content.Shared.StatusEffectNew;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Item;

namespace Content.Shared._Starlight.Magic.Systems;

/// <summary>
///     This handles logic relating to <see cref="BonusDamageComponent" /> and <see cref="BonusDamageStatusEffectComponent" />.
/// </summary>
public sealed class SharedBonusDamageSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BonusDamageStatusEffectComponent, StatusEffectAppliedEvent>(BonusDamageStatusEffectApplied);
        SubscribeLocalEvent<BonusDamageStatusEffectComponent, StatusEffectRemovedEvent>(BonusDamageStatusEffectRemoved);
        SubscribeLocalEvent<BonusDamageComponent, GetMeleeDamageEvent>(OnGetBonusMeleeDamage);
    }

    private void OnGetBonusMeleeDamage(EntityUid uid, BonusDamageComponent component, ref GetMeleeDamageEvent args)
    {
        if (args.User == args.Weapon) {
            if(component.UnarmedBonusDamage != null)
                args.Damage += component.UnarmedBonusDamage;
        }
        else
        {
            if(component.MeleeWeaponBonusDamage != null)
                args.Damage += component.MeleeWeaponBonusDamage;
        }
    }

    private void Recalculate(EntityUid ent, BonusDamageComponent comp)
    {
        // NOTE: ent here may be either the weapon or the wielder. SharedMeleeDamageSystem was altered to accommodate this.
        comp.MeleeWeaponBonusDamage = new();
        // todo: comp.RangedWeaponBonusDamage = new();
        comp.UnarmedBonusDamage = new();
        foreach (var modifier in comp.modifiers)
        {
            if (modifier.Value.AffectsMeleeWeapons)
                comp.MeleeWeaponBonusDamage += modifier.Value.Damage;
            // todo:
            /*if (modifier.Value.AffectsRangedWeapons)
                comp.RangedWeaponBonusDamage += modifier.Value.Damage;*/
            if (modifier.Value.AffectsUnarmed)
                comp.UnarmedBonusDamage += modifier.Value.Damage;
        }
        Dirty(ent, comp);
    }

    private void BonusDamageStatusEffectApplied(EntityUid ent, BonusDamageStatusEffectComponent effect, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp(args.Target, out BonusDamageComponent? component))
            component = AddComp<BonusDamageComponent>(args.Target);

        if (!component.modifiers.ContainsKey(ent) || effect.OverwriteOnRefresh) {
            if (component.modifiers.ContainsKey(ent))
            {
                // refresh the buff only if OverwriteOnRefresh applies and the key was found:
                component.modifiers.Remove(ent);
            }

            // must be careful to copy these by value since future work may make them change dynamically:
            component.modifiers[ent] = new() {
                Damage = new DamageSpecifier(effect.Damage),
                AffectsUnarmed = effect.AffectsUnarmed,
                AffectsMeleeWeapons = effect.AffectsMeleeWeapons //,
                // todo:
                // AffectsRangedWeapons = effect.AffectsRangedWeapons
            };
        }

        Recalculate(args.Target, component);
    }

    private void BonusDamageStatusEffectRemoved(EntityUid ent, BonusDamageStatusEffectComponent effect, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp(args.Target, out BonusDamageComponent? component))
            return;

        if (component.modifiers.ContainsKey(ent))
            component.modifiers.Remove(ent);

        if (component.modifiers.Count == 0)
            RemComp<BonusDamageComponent>(args.Target);
        else
            Recalculate(args.Target, component);
    }
}
