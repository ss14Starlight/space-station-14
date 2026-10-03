using Content.Shared.EntityEffects;
using Content.Shared.StatusEffectNew;
using Content.Shared._Starlight.Sleepiness.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityEffects.Effects.StatusEffects;

public sealed partial class ReduceSleepinessEntityEffectSystem : EntityEffectSystem<MetaDataComponent, ReduceSleepiness>
{
    [Dependency] private StatusEffectsSystem _status = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ReduceSleepiness> args)
    {
        if (args.Effect.Time is not { } time)
            return;

        _status.TryRemoveTime(entity, _sleepinessStatusEffect, time * args.Scale);

        if (_status.TryGetTime(entity, _sleepinessStatusEffect, out var effect) &&
            TryComp<SleepinessStatusEffectComponent>(effect.EffectEnt, out var sleepiness))
        {
            sleepiness.WakeRequested = true;
            Dirty(effect.EffectEnt, sleepiness);
        }
    }

    private static readonly EntProtoId _sleepinessStatusEffect = "StatusEffectSleepiness";
}

public sealed partial class ReduceSleepiness : EntityEffectBase<ReduceSleepiness>
{
    /// <summary>
    /// Duration to remove, in seconds. Defaults to 1 second and is scaled by the effect strength.
    /// </summary>
    [DataField]
    public TimeSpan? Time = TimeSpan.FromSeconds(1);
}
