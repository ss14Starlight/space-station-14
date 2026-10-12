using Content.Shared.Changeling.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Changeling.Systems;

public sealed partial class ChangelingDevourSystem
{
    [Dependency] private MobThresholdSystem _mobThreshold = default!;

    // Cap non-humanoid genetic damage from devour to 90% of their maximum health,
    // paced so the cap lands on the final tick instead of front-loading it.
    // Devour ticks land unmodified (ignoreResistances), so spec values are exact.
    partial void CapDevourDamage(EntityUid target, ChangelingDevourComponent comp, ref DamageSpecifier damage)
    {
        if (HasComp<HumanoidAppearanceComponent>(target))
            return;

        if (!damage.DamageDict.TryGetValue("Cellular", out var tickCellular)
            || tickCellular <= FixedPoint2.Zero
            || !TryComp<DamageableComponent>(target, out var damageable))
            return;

        var cap = comp.DevourConsumeDamageCap;
        if (_mobThreshold.TryGetDeadThreshold(target, out var deadThreshold) && deadThreshold.HasValue)
            cap = Math.Min(deadThreshold.Value.Float() * 0.9f, comp.DevourConsumeDamageCap);

        var totalTicks = comp.DevourConsumeTime.TotalSeconds / comp.DamageTimeBetweenTicks.TotalSeconds;
        var paced = totalTicks > 0 ? FixedPoint2.New(cap / (float) totalTicks) : FixedPoint2.New(cap);

        var currentGenetic = _damageable.GetPositiveDamage((target, damageable), "Genetic").GetTotal();
        var remaining = FixedPoint2.New(cap) - currentGenetic;

        var tickAmount = tickCellular;
        if (paced < tickAmount)
            tickAmount = paced;
        if (remaining < tickAmount)
            tickAmount = remaining;

        damage = new DamageSpecifier(comp.DamagePerTick);
        if (tickAmount <= FixedPoint2.Zero)
            damage.DamageDict.Remove("Cellular");
        else
            damage.DamageDict["Cellular"] = tickAmount;
    }
}
