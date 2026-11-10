using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared._Starlight.Sleepiness.Components;
using Content.Shared._Starlight.Sleepiness.Events;
using Content.Shared._Starlight.Sleepiness.Systems;
using Content.Shared._Starlight.Tiredness.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.FixedPoint;

namespace Content.Server._Starlight.Sleepiness;

public sealed partial class SleepinessSystem : SharedSleepinessSystem
{
    private const float UpdateInterval = 1f;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IRobustRandom _random = default!;

    private float _updateAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateInterval)
            return;

        var updateTime = _updateAccumulator;
        _updateAccumulator = 0f;

        var query = EntityQueryEnumerator<SleepinessStatusEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out var uid, out var sleepiness, out var statusEffect))
        {
            // Let the removal event handle queued effects and preserve pending wake requests.
            if (EntityManager.IsQueuedForDeletion(uid) ||
                statusEffect.AppliedTo is not { } target ||
                statusEffect.EndEffectTime is not { } endTime)
                continue;

            var remaining = endTime - _timing.CurTime;

            if (HasComp<LightSleepComponent>(target) &&
                HasComp<SleepinessInducedSleepComponent>(target) &&
                HasComp<SleepingComponent>(target))
                _blindable.UpdateIsBlind(target);

            if (sleepiness.SleepImmediately && !HasComp<SleepingComponent>(target) &&
                CanInduceSleep(target, sleepiness, checkTirednessSetting: false))
            {
                if (TrySleepFromSleepiness(target, sleepiness.SleepThreshold))
                {
                    sleepiness.SleepImmediately = false;
                    Dirty(uid, sleepiness);
                }

                continue;
            }

            if (HasComp<SleepingComponent>(target))
            {
                sleepiness.SleepResistance += TimeSpan.FromSeconds(updateTime);
                Dirty(uid, sleepiness);
            }

            if (sleepiness.WakeRequested && remaining < sleepiness.SleepThreshold)
            {
                sleepiness.WakeRequested = false;
                if (TryComp<SleepingComponent>(target, out var sleeping))
                {
                    _sleeping.TryWaking((target, sleeping), ignoreSsd: true);
                }
                Dirty(uid, sleepiness);
            }

            if (remaining < sleepiness.SleepThreshold || HasComp<SleepingComponent>(target) ||
                !CanInduceSleep(target, sleepiness,
                    checkTirednessSetting: !sleepiness.HasExternalSleepinessSource))
                continue;

            if (!TrySleepFromSleepiness(target, sleepiness.SleepThreshold))
                continue;
        }
    }

    private bool TrySleepFromSleepiness(EntityUid target, TimeSpan sleepThreshold)
    {
        EnsureComp<SleepinessInducedSleepComponent>(target);
        var sleepCause = Comp<SleepinessInducedSleepComponent>(target);
        sleepCause.SleepThreshold = sleepThreshold;
        Dirty(target, sleepCause);

        if (_sleeping.TrySleeping(target))
            return true;

        RemComp<SleepinessInducedSleepComponent>(target);
        return false;
    }

    private bool CanInduceSleep(
        EntityUid target,
        SleepinessStatusEffectComponent sleepiness,
        bool checkTirednessSetting)
    {
        if (checkTirednessSetting &&
            TryComp<TirednessComponent>(target, out var tiredness) &&
            !tiredness.CanInduceSleep)
            return false;

        if (sleepiness.SleepInductionThreshold <= FixedPoint2.Zero)
            return true;

        if (!TryComp<BloodstreamComponent>(target, out var bloodstream) ||
            !_solutions.ResolveSolution(target, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution,
                out var solution))
            return false;

        if (sleepiness.SleepInductionReagent is not { } reagent)
            return false;

        return solution.GetTotalPrototypeQuantity(reagent) >= sleepiness.SleepInductionThreshold;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SleepinessStatusEffectComponent, StatusEffectRemovedEvent>(OnSleepinessRemoved);
        SubscribeLocalEvent<SleepinessWakeAttemptEvent>(OnWakeAttempt);
        SubscribeLocalEvent<SleepingComponent, ComponentShutdown>(OnSleepingShutdown);
    }

    private void OnSleepingShutdown(EntityUid uid, SleepingComponent component, ComponentShutdown args) =>
        RemoveSleepCauseMarkers(uid);

    private void RemoveSleepCauseMarkers(EntityUid uid)
    {
        RemComp<SleepinessInducedSleepComponent>(uid);
        RemComp<PlayerRequestedSleepComponent>(uid);
    }

    private void OnWakeAttempt(ref SleepinessWakeAttemptEvent args)
    {
        if (!_statusEffects.TryGetTime(args.Target, "StatusEffectSleepiness", out var effect) ||
            !TryComp<SleepinessStatusEffectComponent>(effect.EffectEnt, out var sleepiness))
            return;

        if (effect.EndEffectTime is not { } endTime)
            return;

        var remaining = endTime - _timing.CurTime;
        var power = Math.Max(args.WakePower.TotalSeconds, 0);

        if (power > 0)
        {
            _statusEffects.TryRemoveTime(args.Target, "StatusEffectSleepiness", TimeSpan.FromSeconds(power));

            if (!_statusEffects.TryGetTime(args.Target, "StatusEffectSleepiness", out effect) ||
                effect.EndEffectTime is not { } updatedEndTime)
            {
                args.Result = true;
                return;
            }

            remaining = updatedEndTime - _timing.CurTime;
        }
        var chance = remaining <= TimeSpan.Zero
            ? 1
            : Math.Clamp(power / remaining.TotalSeconds, 0, 1);

        if (!_random.Prob((float) chance))
        {
            args.Result = false;
            return;
        }

        args.Result = true;
    }

    private void OnSleepinessRemoved(Entity<SleepinessStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (ent.Comp.WakeRequested && TryComp<SleepingComponent>(args.Target, out var sleeping))
            _sleeping.TryWaking((args.Target, sleeping), ignoreSsd: true);

        if (HasComp<LightSleepComponent>(args.Target) &&
            HasComp<SleepinessInducedSleepComponent>(args.Target) &&
            HasComp<SleepingComponent>(args.Target))
            _blindable.UpdateIsBlind(args.Target);
    }
}
