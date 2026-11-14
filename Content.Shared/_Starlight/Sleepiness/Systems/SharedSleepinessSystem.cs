using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared._Starlight.Sleepiness.Components;
using Robust.Shared.Timing;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Sleepiness.Systems;

public abstract partial class SharedSleepinessSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _removingSleepinessEffects = [];

    [SubscribeLocalEvent]
    private void OnRefreshMovementSpeed(
        Entity<SleepinessStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (_removingSleepinessEffects.Contains(ent.Owner) ||
            !TryComp<StatusEffectComponent>(ent.Owner, out var statusEffect))
            return;

        var maximumSleepiness = ent.Comp.MaximumSlowdownSleepiness;
        if (maximumSleepiness <= TimeSpan.Zero)
            throw new InvalidOperationException($"Sleepiness slowdown must have a positive cap on {ent.Owner}.");

        var remaining = statusEffect.EndEffectTime is { } endTime
            ? endTime - _timing.CurTime
            : maximumSleepiness;
        var sleepiness = Math.Clamp(remaining.TotalSeconds / maximumSleepiness.TotalSeconds, 0, 1);
        var speedModifier = 1f + ((ent.Comp.MinimumSpeedModifier - 1f) * (float) sleepiness);
        args.Args.ModifySpeed(speedModifier);
    }

    [SubscribeLocalEvent]
    private void OnSleepinessApplied(Entity<SleepinessStatusEffectComponent> _, ref StatusEffectAppliedEvent args) =>
        _movementSpeedModifier.RefreshMovementSpeedModifiers(args.Target);

    [SubscribeLocalEvent]
    private void OnSleepinessEndTimeUpdated(
        Entity<SleepinessStatusEffectComponent> _,
        ref StatusEffectEndTimeUpdatedEvent args) =>
        _movementSpeedModifier.RefreshMovementSpeedModifiers(args.Target);

    protected void RefreshMovementSpeedAfterSleepinessRemoved(EntityUid effect, EntityUid target)
    {
        if (!_removingSleepinessEffects.Add(effect))
            return;

        try
        {
            _movementSpeedModifier.RefreshMovementSpeedModifiers(target);
        }
        finally
        {
            _removingSleepinessEffects.Remove(effect);
        }
    }
}
