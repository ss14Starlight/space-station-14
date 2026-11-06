using Content.Shared.Bed.Sleep;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared._Starlight.Sleepiness.Components;
using Robust.Shared.Timing;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Sleepiness.Systems;

public sealed partial class LightSleepSystem : EntitySystem
{
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;

    [SubscribeLocalEvent]
    private void OnComponentInit(Entity<LightSleepComponent> ent, ref ComponentInit args) =>
        UpdateVision(ent.Owner);

    [SubscribeLocalEvent]
    private void OnComponentRemove(Entity<LightSleepComponent> ent, ref ComponentRemove args) =>
        UpdateVision(ent.Owner);

    [SubscribeLocalEvent]
    private void OnSleepinessInducedSleepInit(Entity<SleepinessInducedSleepComponent> ent, ref ComponentInit args) =>
        UpdateVision(ent.Owner);

    [SubscribeLocalEvent]
    private void OnSleepinessInducedSleepRemove(Entity<SleepinessInducedSleepComponent> ent,
        ref ComponentRemove args) =>
        UpdateVision(ent.Owner);

    [SubscribeLocalEvent]
    private void OnPlayerRequestedSleepInit(Entity<PlayerRequestedSleepComponent> ent, ref ComponentInit args) =>
        UpdateVision(ent.Owner);

    [SubscribeLocalEvent]
    private void OnPlayerRequestedSleepRemove(Entity<PlayerRequestedSleepComponent> ent,
        ref ComponentRemove args) =>
        UpdateVision(ent.Owner);

    public bool IsLightSleepActive(EntityUid uid)
    {
        if (!HasComp<LightSleepComponent>(uid) || !HasComp<SleepingComponent>(uid))
            return false;

        var playerRequestedSleep = HasComp<PlayerRequestedSleepComponent>(uid);
        var hasSleepinessInducedSleep = false;
        var threshold = TimeSpan.Zero;
        if (TryComp<SleepinessInducedSleepComponent>(uid, out var sleepCause))
        {
            hasSleepinessInducedSleep = true;
            threshold = sleepCause.SleepThreshold;
        }
        else if (!playerRequestedSleep)
        {
            return false;
        }

        var remaining = TimeSpan.Zero;
        if (_statusEffects.TryGetTime(uid, "StatusEffectSleepiness", out var effect))
        {
            if (effect.EndEffectTime is not { } endTime)
                return false;

            remaining = endTime - _timing.CurTime;
            if (!hasSleepinessInducedSleep)
            {
                if (!TryComp<SleepinessStatusEffectComponent>(effect.EffectEnt, out var sleepiness))
                    return false;

                threshold = sleepiness.SleepThreshold;
            }
        }

        return remaining <= threshold;
    }

    private void UpdateVision(EntityUid uid)
    {
        if (HasComp<SleepingComponent>(uid))
            _blindable.UpdateIsBlind(uid);
    }
}
