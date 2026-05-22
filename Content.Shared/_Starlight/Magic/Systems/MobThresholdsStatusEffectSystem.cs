using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared._Starlight.Magic.Components;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Shared._Starlight.Magic.Systems;

public sealed class MobThresholdStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobthresholds = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobThresholdsStatusEffectComponent, StatusEffectRemovedEvent>(MobThresholdsStatusEffectRemoved);
        SubscribeLocalEvent<MobThresholdsStatusEffectComponent, StatusEffectAppliedEvent>(MobThresholdsStatusEffectApplied);
    }

    private void ApplyConsequences(EntityUid effectTarget, ref MobThresholdsComponent thresholds, MobState oldState)
    {
        // changing thresholds should not be able to bring an entity back to life,
        // but should otherwise affect MobState - hopefully HealEvenly will do that for us?

        if (oldState != MobState.Dead)
        {
            DamageableComponent? dc = null;
            if (!Resolve(effectTarget, ref dc))
                return;
            _damageable.HealEvenly(effectTarget, 0);
        }
    }

    private void MobThresholdsStatusEffectApplied(EntityUid target, MobThresholdsStatusEffectComponent thresholdsEffect, ref StatusEffectAppliedEvent args)
    {
        MobThresholdsComponent? thresholds = null;
        MobStateComponent? msc = null;

        if (!Resolve(args.Target, ref thresholds, ref msc))
            return;

        MobState oldState = msc.CurrentState;

        // refuse to apply a new MobThresholdsStatusEffect if one is already applied:
        if(thresholds.OriginalThresholds.Count > 0)
            return;

        // as in MobThresholdSystem's SetMobStateThreshold, we need to clone this dictionary since we'll be mutating it later:
        thresholds.OriginalThresholds = new SortedDictionary<FixedPoint2, MobState>(thresholds.Thresholds);
        thresholds.OriginalAllowRevives = thresholds.AllowRevives;

        if (thresholds.AllowRevives != thresholdsEffect.AllowRevives)
            _mobthresholds.SetAllowRevives(args.Target, thresholdsEffect.AllowRevives, thresholds);

        foreach (var (damage, name) in thresholdsEffect.Thresholds)
        {
            _mobthresholds.SetMobStateThreshold(args.Target, damage, name, thresholds);
        }

        ApplyConsequences(args.Target, ref thresholds, oldState);
    }

    private void MobThresholdsStatusEffectRemoved(EntityUid target, MobThresholdsStatusEffectComponent thresholdsEffect, ref StatusEffectRemovedEvent args)
    {
        MobThresholdsComponent? thresholds = null;
        MobStateComponent? msc = null;

        if (!Resolve(args.Target, ref thresholds, ref msc))
            return;

        // thresholds.Thresholds = thresholdsEffect.OriginalThresholds;
        // thresholds.AllowRevives = thresholdsEffect.OriginalAllowRevives;

        if (thresholds.OriginalAllowRevives != thresholds.AllowRevives)
            _mobthresholds.SetAllowRevives(args.Target, thresholds.OriginalAllowRevives, thresholds);

        foreach (var (damage, name) in thresholds.OriginalThresholds)
        {
            _mobthresholds.SetMobStateThreshold(args.Target, damage, name, thresholds);
        }

        // remove all values, indicating another buff can be reapplied later:
        thresholds.OriginalThresholds.Clear();

        MobState oldState = msc.CurrentState;

        ApplyConsequences(args.Target, ref thresholds, oldState);
    }
}
