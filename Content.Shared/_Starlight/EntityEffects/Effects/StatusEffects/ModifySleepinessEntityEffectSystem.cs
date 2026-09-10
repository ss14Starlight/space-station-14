using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.EntityEffects.Effects.StatusEffects;

public sealed partial class ModifySleepinessEntityEffectSystem : EntityEffectSystem<MetaDataComponent, ModifySleepiness>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ModifySleepiness> args)
    {
        if (args.Effect.Time is not { } time)
            return;

        time *= args.Scale;
        if (time <= TimeSpan.Zero || args.Effect.MaximumSleepiness <= TimeSpan.Zero)
            return;

        if (!_status.TryGetTime(entity, args.Effect.EffectProto, out var current))
        {
            _status.TryAddStatusEffectDuration(entity, args.Effect.EffectProto,
                time <= args.Effect.MaximumSleepiness ? time : args.Effect.MaximumSleepiness);
            return;
        }

        if (current.EndEffectTime is not { } endTime)
            return;

        var currentDuration = endTime - _timing.CurTime;
        if (currentDuration >= args.Effect.MaximumSleepiness)
            return;

        var newDuration = currentDuration < TimeSpan.Zero ? time : currentDuration + time;
        if (newDuration > args.Effect.MaximumSleepiness)
            newDuration = args.Effect.MaximumSleepiness;

        if (newDuration <= currentDuration)
            return;

        _status.TrySetStatusEffectDuration(entity, args.Effect.EffectProto, newDuration);
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
}
