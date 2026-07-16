using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.AlertAwareArmor;
using Content.Shared.Armor;
using Content.Shared.Cargo;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.AlertAwareArmor;

/// <summary>
/// Applies resistances based on the alert level.
/// </summary>
public sealed class AlertAwareArmorSystem : SharedAlertAwareArmorSystem
{
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertAwareArmorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
        SubscribeLocalEvent<AlertAwareArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<AlertAwareArmorComponent, InventoryRelayedEvent<StaminaModifyEvent>>(OnStaminaDamageModify);
        SubscribeLocalEvent<AlertAwareArmorComponent, InventoryRelayedEvent<KnockDownAttemptEvent>>(OnKnockdownAttempt);
        SubscribeLocalEvent<AlertAwareArmorComponent, BorgModuleRelayedEvent<DamageModifyEvent>>(OnBorgDamageModify);
        SubscribeLocalEvent<AlertAwareArmorComponent, PriceCalculationEvent>(GetArmorPrice);
    }

    private AlertArmorLevel? GetActiveLevel(EntityUid uid, AlertAwareArmorComponent component)
    {
        if (_station.GetOwningStation(uid) is { } station
            && _alertLevel.GetLevel(station) is { Length: > 0 } level
            && component.Levels.TryGetValue(level, out var data))
            return data;

        return null;
    }

    private DamageModifierSet GetActiveModifiers(EntityUid uid, AlertAwareArmorComponent component)
        => GetActiveLevel(uid, component)?.Modifiers ?? component.Modifiers;

    private void OnCoefficientQuery(Entity<AlertAwareArmorComponent> ent, ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        if (TryComp<MaskComponent>(ent, out var mask) && mask.IsToggled)
            return;

        var modifiers = GetActiveModifiers(ent, ent.Comp);

        foreach (var armorCoefficient in modifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[armorCoefficient.Key] = args.Args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient) ? coefficient * armorCoefficient.Value : armorCoefficient.Value;
        }
    }

    private void OnDamageModify(EntityUid uid, AlertAwareArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        var modifiers = GetActiveModifiers(uid, component);

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, modifiers, args.Args.ArmorPenetration, args.Args.CanHeal);
    }

    private void OnStaminaDamageModify(EntityUid uid, AlertAwareArmorComponent component, InventoryRelayedEvent<StaminaModifyEvent> args)
    {
        if (args.Args.Damage < 0)
            return;

        var level = GetActiveLevel(uid, component);
        var modifier = level?.StaminaDamageModifier ?? component.StaminaDamageModifier;

        if (args.Args.Modifier > modifier)
            args.Args.Modifier = modifier;
    }

    private void OnKnockdownAttempt(EntityUid uid, AlertAwareArmorComponent component, InventoryRelayedEvent<KnockDownAttemptEvent> args)
    {
        var level = GetActiveLevel(uid, component);
        var ignoreKnockdown = level?.IgnoreKnockdown ?? component.IgnoreKnockdown;

        if (ignoreKnockdown && !args.Args.Voluntary)
            args.Args.Cancelled = true;
    }

    private void OnBorgDamageModify(EntityUid uid, AlertAwareArmorComponent component,
        ref BorgModuleRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        var modifiers = GetActiveModifiers(uid, component);

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, modifiers, args.Args.ArmorPenetration, args.Args.CanHeal);
    }

    private void GetArmorPrice(EntityUid uid, AlertAwareArmorComponent component, ref PriceCalculationEvent args)
    {
        foreach (var modifier in component.Modifiers.Coefficients)
        {
            var damageType = _protoManager.Index<DamageTypePrototype>(modifier.Key);
            args.Price += component.PriceMultiplier * damageType.ArmorPriceCoefficient * 100 * (1 - modifier.Value);
        }

        foreach (var modifier in component.Modifiers.FlatReduction)
        {
            var damageType = _protoManager.Index<DamageTypePrototype>(modifier.Key);
            args.Price += component.PriceMultiplier * damageType.ArmorPriceFlat * modifier.Value;
        }
    }
}
