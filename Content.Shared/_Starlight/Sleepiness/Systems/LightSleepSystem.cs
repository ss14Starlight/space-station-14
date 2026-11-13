using Content.Shared.Bed.Sleep;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared._Starlight.Sleepiness.Components;
using Robust.Shared.Timing;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Sleepiness.Systems;

public sealed partial class LightSleepSystem : EntitySystem
{
    private static readonly TimeSpan _voluntaryWakePower = TimeSpan.FromSeconds(1);

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

    /// <summary>
    /// Checks whether an entity is voluntarily or sleepiness-induced sleeping, is not forced asleep,
    /// and is within the boundary where voluntary waking is guaranteed to succeed.
    /// </summary>
    public bool IsLightSleepActive(Entity<LightSleepComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, logMissing: false) || !HasComp<SleepingComponent>(ent))
            return false;

        var uid = ent.Owner;
        var playerRequestedSleep = HasComp<PlayerRequestedSleepComponent>(uid);
        var hasSleepinessInducedSleep = HasComp<SleepinessInducedSleepComponent>(uid);
        if (!playerRequestedSleep && !hasSleepinessInducedSleep)
            return false;

        if (_statusEffects.HasEffectComp<ForcedSleepingStatusEffectComponent>(uid))
            return false;

        if (!_statusEffects.HasEffectComp<SleepinessStatusEffectComponent>(uid))
            return true;

        if (!_statusEffects.TryGetTime(uid, "StatusEffectSleepiness", out var effect) ||
            effect.EndEffectTime is not { } endTime)
            return false;

        // SleepinessSystem removes wake power before rolling, so this is the guaranteed-success boundary.
        var remainingAfterWake = endTime - _timing.CurTime - _voluntaryWakePower;
        return remainingAfterWake <= _voluntaryWakePower;
    }

    private void UpdateVision(EntityUid uid)
    {
        if (HasComp<SleepingComponent>(uid))
            _blindable.UpdateIsBlind(uid);
    }
}
