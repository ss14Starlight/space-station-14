using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;

namespace Content.Server._Starlight.Legendary.Modifiers;

public sealed class LegendaryArmorBonusSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LegendaryArmorBonusComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
        SubscribeLocalEvent<LegendaryArmorBonusComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
    }

    private void OnCoefficientQuery(Entity<LegendaryArmorBonusComponent> ent, ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        foreach (var bonusCoefficient in ent.Comp.Modifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[bonusCoefficient.Key] =
                args.Args.DamageModifiers.Coefficients.TryGetValue(bonusCoefficient.Key, out var existing)
                    ? existing * bonusCoefficient.Value
                    : bonusCoefficient.Value;
        }
    }

    private void OnDamageModify(Entity<LegendaryArmorBonusComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(
            args.Args.Damage,
            ent.Comp.Modifiers,
            args.Args.ArmorPenetration,
            args.Args.CanHeal);
    }
}
