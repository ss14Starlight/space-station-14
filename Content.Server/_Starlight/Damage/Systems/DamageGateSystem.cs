using Content.Server._Starlight.Damage.Components;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared._Starlight.Medical.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
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

        if (component.MinDamageToStage is { } minDamageToStage && minDamageToStage < FixedPoint2.Zero)
        {
            Log.Error(
                $"DamageGateComponent on {uid} has a negative MinDamageToStage; disabling that minimum.");
            component.MinDamageToStage = null;
        }

        if (component.MinDamageToDestruction is { } minDamageToDestruction
            && minDamageToDestruction < FixedPoint2.Zero)
        {
            Log.Error(
                $"DamageGateComponent on {uid} has a negative MinDamageToDestruction; disabling that minimum.");
            component.MinDamageToDestruction = null;
        }

        if (component.DamageCooldown < TimeSpan.Zero)
        {
            Log.Error($"DamageGateComponent on {uid} has a negative DamageCooldown; disabling that cooldown.");
            component.DamageCooldown = TimeSpan.Zero;
        }

        if (component.StageDamageCooldown < TimeSpan.Zero)
        {
            Log.Error(
                $"DamageGateComponent on {uid} has a negative StageDamageCooldown; disabling that cooldown.");
            component.StageDamageCooldown = TimeSpan.Zero;
        }

        if (!HasComp<DamageableComponent>(uid))
            Log.Warning($"DamageGateComponent on {uid} requires a DamageableComponent.");

        var usesStageThresholds = component.LimitToNextStage
            || component.StageDamageCooldown > TimeSpan.Zero
            || component.MinDamageToStage is not null
            || component.MinDamageToDestruction is not null;
        if (!TryComp<DestructibleComponent>(uid, out var destructible))
        {
            if (usesStageThresholds)
                Log.Warning(
                    $"DamageGateComponent on {uid} uses Destructible thresholds, but has no DestructibleComponent.");

            return;
        }

        if (component.MinDamageToStage is not null || component.MinDamageToDestruction is not null)
            ValidateMinimumStageThresholds(uid, component, destructible);
    }

    private void OnDamageAfterModifiers(EntityUid uid, DamageGateComponent component, DamageAfterModifiersEvent args)
    {
        args.Damage = args.Damage.Clone();
        var maxDamage = component.MaxDamagePerHit;

        if (IsCooldownActive(component.DamageCooldown, component.DamageCooldownUntil)
            || IsCooldownActive(component.StageDamageCooldown, component.StageDamageCooldownUntil))
        {
            LimitPositiveDamage(uid, args.Damage, FixedPoint2.Zero);
            return;
        }

        var hitDamage = GetTotalPositiveDamage(uid, args.Damage);
        if (maxDamage is { } maxDamagePerHit && hitDamage > maxDamagePerHit.Value)
            hitDamage = maxDamagePerHit.Value;

        if (TryComp<DestructibleComponent>(uid, out var destructible)
            && HasComp<DamageableComponent>(uid))
        {
            var currentDamage = _damageable.GetTotalDamage(uid);
            var totalDamageAfterHit = currentDamage.Value + hitDamage;

            if (component.LimitToNextStage
                && TryGetNextStageThreshold(destructible, currentDamage, out var nextThreshold))
            {
                var thresholdDamage = nextThreshold - currentDamage;
                if (maxDamage is null || thresholdDamage < maxDamage.Value)
                    maxDamage = thresholdDamage;
            }

            foreach (var threshold in destructible.Thresholds)
            {
                if (threshold.Trigger is not DamageTrigger damageTrigger
                    || damageTrigger.Damage <= currentDamage
                    || damageTrigger.Damage.Value > totalDamageAfterHit)
                    continue;

                var minimumHitDamage = GetMinimumHitDamage(component, threshold);
                if (minimumHitDamage is null || hitDamage >= minimumHitDamage.Value.Value)
                    continue;

                var damageBeforeThreshold = damageTrigger.Damage - currentDamage - FixedPoint2.FromCents(1);
                if (damageBeforeThreshold < FixedPoint2.Zero)
                    damageBeforeThreshold = FixedPoint2.Zero;

                if (maxDamage is null || damageBeforeThreshold < maxDamage.Value)
                    maxDamage = damageBeforeThreshold;
            }
        }

        if (maxDamage is { } limit)
            LimitPositiveDamage(uid, args.Damage, limit);
    }

    private void OnDamageChanged(EntityUid uid, DamageGateComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (component.DamageCooldown > TimeSpan.Zero)
            component.DamageCooldownUntil = _timing.CurTime + component.DamageCooldown;

        if (component.StageDamageCooldown <= TimeSpan.Zero
            || !TryComp<DestructibleComponent>(uid, out var destructible)
            || !ReachedNextStage(uid, destructible, args))
            return;

        component.StageDamageCooldownUntil = _timing.CurTime + component.StageDamageCooldown;
    }

    private bool IsCooldownActive(TimeSpan cooldown, TimeSpan? cooldownUntil) =>
        cooldown > TimeSpan.Zero && cooldownUntil is { } until && _timing.CurTime < until;

    private bool ReachedNextStage(EntityUid uid, DestructibleComponent destructible, DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } damageDelta)
            return false;

        var currentDamage = _damageable.GetTotalDamage(uid);
        var previousDamage = currentDamage - damageDelta.GetTotal();

        return TryGetNextStageThreshold(destructible, previousDamage, out var nextThreshold)
            && currentDamage >= nextThreshold;
    }

    private static bool TryGetNextStageThreshold(
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

    private static FixedPoint2? GetMinimumHitDamage(
        DamageGateComponent component,
        DamageThreshold threshold)
    {
        FixedPoint2? minimumHitDamage = null;
        foreach (var behavior in threshold.Behaviors)
        {
            if (behavior is not DoActsBehavior actsBehavior)
                continue;

            if (component.MinDamageToStage is { } minDamageToStage
                && actsBehavior.HasAct(ThresholdActs.Breakage)
                && (minimumHitDamage is null || minDamageToStage > minimumHitDamage.Value))
                minimumHitDamage = minDamageToStage;

            if (component.MinDamageToDestruction is { } minDamageToDestruction
                && actsBehavior.HasAct(ThresholdActs.Destruction)
                && (minimumHitDamage is null || minDamageToDestruction > minimumHitDamage.Value))
                minimumHitDamage = minDamageToDestruction;
        }

        return minimumHitDamage;
    }

    private void ValidateMinimumStageThresholds(
        EntityUid uid,
        DamageGateComponent component,
        DestructibleComponent destructible)
    {
        var foundStageThreshold = false;
        var foundDestructionThreshold = false;

        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is not DamageTrigger)
                continue;

            foreach (var behavior in threshold.Behaviors)
            {
                if (behavior is not DoActsBehavior actsBehavior)
                    continue;

                if (component.MinDamageToStage is not null
                    && actsBehavior.HasAct(ThresholdActs.Breakage))
                    foundStageThreshold = true;

                if (component.MinDamageToDestruction is not null
                    && actsBehavior.HasAct(ThresholdActs.Destruction))
                    foundDestructionThreshold = true;
            }
        }

        if (component.MinDamageToStage is not null && !foundStageThreshold)
            Log.Warning(
                $"DamageGateComponent on {uid} has MinDamageToStage configured, but no DamageTrigger with a Breakage action was found.");

        if (component.MinDamageToDestruction is not null && !foundDestructionThreshold)
            Log.Warning(
                $"DamageGateComponent on {uid} has MinDamageToDestruction configured, but no DamageTrigger with a Destruction action was found.");
    }

    private void LimitPositiveDamage(EntityUid uid, DamageSpecifier damage, FixedPoint2 maximum)
    {
        var damageTypes = new List<ProtoId<DamageTypePrototype>>();
        long totalPositiveDamage = 0;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (value <= FixedPoint2.Zero || !_damageable.CanBeDamagedBy(uid, type))
                continue;

            totalPositiveDamage += value.Value;
            damageTypes.Add(type);
        }

        if (totalPositiveDamage <= maximum.Value)
            return;

        long remainingMaximum = maximum.Value;
        var remainingDamage = totalPositiveDamage;
        foreach (var type in damageTypes)
        {
            var value = damage.DamageDict[type];
            var scaledValue = (long)value.Value * remainingMaximum / remainingDamage;
            damage.DamageDict[type] = FixedPoint2.FromCents((int)scaledValue);
            remainingMaximum -= scaledValue;
            remainingDamage -= value.Value;
        }
    }

    private long GetTotalPositiveDamage(EntityUid uid, DamageSpecifier damage)
    {
        long totalPositiveDamage = 0;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (value > FixedPoint2.Zero && _damageable.CanBeDamagedBy(uid, type))
                totalPositiveDamage += value.Value;
        }

        return totalPositiveDamage;
    }
}
