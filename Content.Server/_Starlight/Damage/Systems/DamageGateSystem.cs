using Content.Server._Starlight.Damage.Components;
using Content.Server.Destructible;
using Content.Shared._Starlight.Medical.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Damage.Systems;

public sealed partial class DamageGateSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private DamageableSystem _damageable = null!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageGateComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DamageGateComponent, DamageAfterModifiersEvent>(OnDamageAfterModifiers);
        SubscribeLocalEvent<DamageGateComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnComponentInit(EntityUid uid, DamageGateComponent component, ComponentInit args)
    {
        if (component.MaxDamagePerHit is { } maxDamage && maxDamage < FixedPoint2.Zero)
        {
            Log.Error($"DamageGateComponent on {uid} has a negative MaxDamagePerHit; disabling that limit.");
            component.MaxDamagePerHit = null;
        }

        if (component.DamageCooldown < TimeSpan.Zero)
        {
            Log.Error($"DamageGateComponent on {uid} has a negative DamageCooldown; disabling that cooldown.");
            component.DamageCooldown = TimeSpan.Zero;
        }

        if (component.ThresholdDamageCooldown < TimeSpan.Zero)
        {
            Log.Error(
                $"DamageGateComponent on {uid} has a negative ThresholdDamageCooldown; disabling that cooldown.");
            component.ThresholdDamageCooldown = TimeSpan.Zero;
        }

        if (!HasComp<DamageableComponent>(uid))
            Log.Warning($"DamageGateComponent on {uid} requires a DamageableComponent.");

        if ((component.LimitToNextDestructionThreshold || component.ThresholdDamageCooldown > TimeSpan.Zero)
            && !HasComp<DestructibleComponent>(uid))
            Log.Warning(
                $"DamageGateComponent on {uid} uses Destructible thresholds, but has no DestructibleComponent.");
    }

    private void OnDamageAfterModifiers(EntityUid uid, DamageGateComponent component, DamageAfterModifiersEvent args)
    {
        args.Damage = args.Damage.Clone();
        var maxDamage = component.MaxDamagePerHit;

        if (IsCooldownActive(component.DamageCooldown, component.DamageCooldownUntil)
            || IsCooldownActive(component.ThresholdDamageCooldown, component.ThresholdDamageCooldownUntil))
        {
            LimitPositiveDamage(args.Damage, FixedPoint2.Zero);
            return;
        }

        if (component.LimitToNextDestructionThreshold
            && TryComp<DestructibleComponent>(uid, out var destructible)
            && HasComp<DamageableComponent>(uid))
        {
            var currentDamage = _damageable.GetTotalDamage(uid);
            if (TryGetNextDestructionThreshold(destructible, currentDamage, out var nextThreshold))
            {
                var thresholdDamage = nextThreshold - currentDamage;
                if (maxDamage is null || thresholdDamage < maxDamage.Value)
                    maxDamage = thresholdDamage;
            }
        }

        if (maxDamage is { } limit)
            LimitPositiveDamage(args.Damage, limit);
    }

    private void OnDamageChanged(EntityUid uid, DamageGateComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (component.DamageCooldown > TimeSpan.Zero)
            component.DamageCooldownUntil = _timing.CurTime + component.DamageCooldown;

        if (component.ThresholdDamageCooldown <= TimeSpan.Zero
            || !TryComp<DestructibleComponent>(uid, out var destructible)
            || !ReachedNextThreshold(uid, destructible, args))
            return;

        component.ThresholdDamageCooldownUntil = _timing.CurTime + component.ThresholdDamageCooldown;
    }

    private bool IsCooldownActive(TimeSpan cooldown, TimeSpan? cooldownUntil) =>
        cooldown > TimeSpan.Zero && cooldownUntil is { } until && _timing.CurTime < until;

    private bool ReachedNextThreshold(EntityUid uid, DestructibleComponent destructible, DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } damageDelta)
            return false;

        var currentDamage = _damageable.GetTotalDamage(uid);
        var previousDamage = currentDamage - damageDelta.GetTotal();

        return TryGetNextDestructionThreshold(destructible, previousDamage, out var nextThreshold)
            && currentDamage >= nextThreshold;
    }

    private static bool TryGetNextDestructionThreshold(
        DestructibleComponent destructible,
        FixedPoint2 currentDamage,
        out FixedPoint2 nextThreshold)
    {
        nextThreshold = default;
        var found = false;

        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is not DamageTrigger damageTrigger || damageTrigger.Damage <= currentDamage)
                continue;

            if (found && damageTrigger.Damage >= nextThreshold)
                continue;

            nextThreshold = damageTrigger.Damage;
            found = true;
        }

        return found;
    }

    private static void LimitPositiveDamage(DamageSpecifier damage, FixedPoint2 maximum)
    {
        var totalPositiveDamage = FixedPoint2.Zero;
        foreach (var value in damage.DamageDict.Values)
            if (value > FixedPoint2.Zero)
                totalPositiveDamage += value;

        if (totalPositiveDamage <= maximum)
            return;

        var damageTypes = new List<ProtoId<DamageTypePrototype>>(damage.DamageDict.Keys);
        var remainingMaximum = maximum;
        var remainingDamage = totalPositiveDamage;
        foreach (var type in damageTypes)
        {
            var value = damage.DamageDict[type];
            if (value <= FixedPoint2.Zero)
                continue;

            var scaledValue = FixedPoint2.Min(value * remainingMaximum / remainingDamage, remainingMaximum);
            damage.DamageDict[type] = scaledValue;
            remainingMaximum -= scaledValue;
            remainingDamage -= value;
        }
    }
}
