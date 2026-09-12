using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared._Starlight.Sleepiness.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.FixedPoint;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared._Starlight.EntityEffects.Effects.StatusEffects;

public sealed partial class ModifySleepinessEntityEffectSystem : EntityEffectSystem<MetaDataComponent, ModifySleepiness>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ModifySleepiness> args)
    {
        if (args.Effect.Time is not { } effectTime)
            return;

        var time = effectTime * args.Scale;
        var maximumTime = effectTime * 1.5;
        if (time > maximumTime)
            time = maximumTime;

        if (time <= TimeSpan.Zero || args.Effect.MaximumSleepiness <= TimeSpan.Zero)
            return;

        if (!_status.TryGetTime(entity, args.Effect.EffectProto, out var current))
        {
            var initialTime = GetResistedTime(entity, time, args.Effect.MaximumSleepiness,
                args.Effect.FullResistanceAfter, out _, out _);
            if (initialTime <= TimeSpan.Zero ||
                !_status.TryAddStatusEffectDuration(entity, args.Effect.EffectProto, initialTime))
                return;
            SetSleepInductionRequirement(entity, args.Effect);
            return;
        }

        if (current.EndEffectTime is not { } endTime)
            return;

        SetSleepInductionRequirement(current.EffectEnt, args.Effect);

        var currentDuration = endTime - _timing.CurTime;
        if (args.Effect.OnlyAfterSleepThreshold &&
            (!TryComp<SleepinessStatusEffectComponent>(current.EffectEnt, out var sleepiness) ||
             currentDuration < sleepiness.SleepThreshold))
            return;

        if (currentDuration >= args.Effect.MaximumSleepiness)
            return;

        var resistedTime = GetResistedTime(current.EffectEnt, time, args.Effect.MaximumSleepiness,
            args.Effect.FullResistanceAfter, out _, out _);
        var newDuration = currentDuration < TimeSpan.Zero ? resistedTime : currentDuration + resistedTime;
        if (newDuration > args.Effect.MaximumSleepiness)
            newDuration = args.Effect.MaximumSleepiness;

        if (newDuration <= currentDuration)
            return;

        _status.TrySetStatusEffectDuration(entity, args.Effect.EffectProto, newDuration);
    }

    private TimeSpan GetResistedTime(EntityUid entity, TimeSpan time, TimeSpan maximumSleepiness,
        TimeSpan fullResistanceAfter, out double resistance, out double ratio)
    {
        resistance = 0;
        ratio = 0;
        if (!TryComp<SleepinessStatusEffectComponent>(entity, out var sleepiness) || fullResistanceAfter <= TimeSpan.Zero)
            return time <= maximumSleepiness ? time : maximumSleepiness;

        resistance = sleepiness.SleepResistance.TotalSeconds;
        ratio = Math.Clamp(resistance / fullResistanceAfter.TotalSeconds, 0, 1);
        var resisted = time.TotalSeconds * (1 - ratio);
        return TimeSpan.FromSeconds(Math.Min(resisted, maximumSleepiness.TotalSeconds));
    }

    private void SetSleepInductionRequirement(EntityUid effect, ModifySleepiness sleepiness)
    {
        if (sleepiness.SleepInductionThreshold <= FixedPoint2.Zero ||
            sleepiness.SleepInductionReagent is not { } reagent ||
            !TryComp<SleepinessStatusEffectComponent>(effect, out var component))
            return;

        component.SleepInductionReagent = reagent;
        component.SleepInductionThreshold = sleepiness.SleepInductionThreshold;
        Dirty(effect, component);
    }
}

public sealed partial class ModifySleepiness : EntityEffectBase<ModifySleepiness>
{
    [DataField(required: true)]
    public EntProtoId EffectProto;

    [DataField(required: true)]
    public TimeSpan MaximumSleepiness;

    [DataField]
    public TimeSpan? Time = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan FullResistanceAfter = TimeSpan.FromMinutes(15);

    [DataField]
    public bool OnlyAfterSleepThreshold;

    [DataField]
    public ProtoId<ReagentPrototype>? SleepInductionReagent;

    [DataField]
    public FixedPoint2 SleepInductionThreshold = FixedPoint2.Zero;
}
