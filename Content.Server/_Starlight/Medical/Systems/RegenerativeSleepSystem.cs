using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._Starlight.Medical.Components;
using Content.Shared._Starlight.Sleepiness.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Systems;

public sealed partial class RegenerativeSleepSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private bool CanHealPassive(EntityUid uid)
    {
        if (!_mobState.IsAlive(uid)) return false;
        if (!HasComp<SleepingComponent>(uid)) return true;

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

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<RegenerativeSleepComponent>();
        while (query.MoveNext(out var uid, out var regenerativeSleep))
        {
            if (regenerativeSleep.NextHeal > curTime)
                continue;

            if(!CanHealPassive(uid))
                continue;

            var sleeping = HasComp<SleepingComponent>(uid);
            var holder = GetHolder(uid);
            var selfHealing = sleeping && IsDamaged(uid);
            var purringForHolder = false;

            if (selfHealing)
                Heal(uid, uid, regenerativeSleep);

            if (holder is { } holderUid)
            {
                purringForHolder = IsDamaged(holderUid);
                Heal(holderUid, uid, regenerativeSleep);
            }

            if (selfHealing || purringForHolder)
                _chat.TryEmoteWithoutChat(uid, "Purr", ignoreActionBlocker: sleeping);

            regenerativeSleep.NextHeal = curTime + regenerativeSleep.UpdateInterval;
        }
    }

    private EntityUid? GetHolder(EntityUid uid)
    {
        if (!_container.TryGetContainingContainer((uid, null, null), out var container)
            || !_hands.IsHolding((container.Owner, null), uid))
            return null;

        return container.Owner;
    }

    private bool IsDamaged(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return false;

        return _damageable.GetTotalDamage((uid, damageable)) > FixedPoint2.Zero;
    }

    private void Heal(EntityUid target, EntityUid origin, RegenerativeSleepComponent regenerativeSleep)
    {
        _damageable.TryChangeDamage(target, -regenerativeSleep.HealingPerUpdate, true, origin: origin);
        _bloodstream.TryModifyBleedAmount(target, -regenerativeSleep.BleedHealPerUpdate);
    }
}
