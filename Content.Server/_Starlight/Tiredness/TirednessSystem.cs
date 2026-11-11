using System.Collections.Generic;
using Content.Shared.Alert;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Content.Shared._Starlight.Sleepiness.Components;
using Content.Shared._Starlight.Tiredness.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Tiredness;

public sealed partial class TirednessSystem : EntitySystem
{
    private const float UpdateInterval = 1f;

    // At full tiredness, net sleepiness accumulation is 0.25 seconds per second.
    private const float MaximumSleepinessSecondsPerSecond = 1.25f;
    private const float SleepRecoveryMultiplier = 15f;
    public static readonly TimeSpan MaximumTiredness = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan _alertThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan _severeAlertThreshold = TimeSpan.FromMinutes(25);
    private static readonly ProtoId<AlertCategoryPrototype> _alertCategory = "Tiredness";
    private static readonly ProtoId<AlertPrototype> _alert = "FelionoidTiredness";
    private static readonly EntProtoId _sleepinessStatusEffect = "StatusEffectSleepiness";

    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private ActionContainerSystem _actionContainers = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _sleepinessApplicationFailures = new();
    private float _updateAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateInterval)
            return;

        var updateTime = _updateAccumulator;
        _updateAccumulator = 0f;
        var maximumTirednessSeconds = MaximumTiredness.TotalSeconds;

        var query = EntityQueryEnumerator<TirednessComponent>();
        while (query.MoveNext(out var uid, out var tiredness))
        {
            if (_mobState.IsDead(uid))
                continue;

            var oldTirednessSeconds = tiredness.CurrentTiredness.TotalSeconds;
            var isSleeping = HasComp<SleepingComponent>(uid);
            var tirednessDelta = isSleeping ? -updateTime * SleepRecoveryMultiplier : updateTime;
            var newTirednessSeconds = Math.Clamp(oldTirednessSeconds + tirednessDelta, 0, maximumTirednessSeconds);
            if (newTirednessSeconds != oldTirednessSeconds)
            {
                tiredness.CurrentTiredness = TimeSpan.FromSeconds(newTirednessSeconds);
                DirtyField(uid, tiredness, nameof(TirednessComponent.CurrentTiredness));
            }

            UpdateAlert(uid, tiredness);

            if (isSleeping)
                continue;

            var averageTirednessRatio = Math.Clamp(
                (oldTirednessSeconds + newTirednessSeconds) / (2 * maximumTirednessSeconds),
                0,
                1);
            var sleepinessSecondsPerSecond = (float)averageTirednessRatio * MaximumSleepinessSecondsPerSecond;
            if (sleepinessSecondsPerSecond <= 0f)
                continue;

            if (sleepinessSecondsPerSecond <= 1f &&
                !_statusEffects.HasEffectComp<SleepinessStatusEffectComponent>(uid))
                continue;

            var currentSleepinessSeconds = 0d;
            if (_statusEffects.TryGetTime(uid, _sleepinessStatusEffect, out var sleepiness))
            {
                if (sleepiness.EndEffectTime is not { } endTime)
                    continue;

                currentSleepinessSeconds = Math.Max(0, (endTime - _timing.CurTime).TotalSeconds);
            }

            var availableSleepinessSeconds = tiredness.MaximumSleepiness.TotalSeconds - currentSleepinessSeconds;
            if (availableSleepinessSeconds <= 0)
                continue;

            var sleepinessSeconds = Math.Min(
                sleepinessSecondsPerSecond * updateTime,
                availableSleepinessSeconds);
            if (sleepinessSeconds <= 0)
                continue;

            var sleepinessDuration = TimeSpan.FromSeconds(sleepinessSeconds);
            if (!_statusEffects.TryAddStatusEffectDuration(uid, _sleepinessStatusEffect, sleepinessDuration) &&
                _sleepinessApplicationFailures.Add(uid))
            {
                Log.Error($"Failed to add {_sleepinessStatusEffect} for tired entity {uid}.");
            }
        }
    }

    /// <summary>
    /// Sets an entity's current tiredness.
    /// </summary>
    /// <param name="ent">The entity and its optional tiredness component.</param>
    /// <param name="tiredness">The tiredness value to set.</param>
    public void SetTiredness(Entity<TirednessComponent?> ent, TimeSpan tiredness)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var newTiredness = TimeSpan.FromSeconds(Math.Clamp(tiredness.TotalSeconds, 0, MaximumTiredness.TotalSeconds));
        if (newTiredness != ent.Comp.CurrentTiredness)
        {
            ent.Comp.CurrentTiredness = newTiredness;
            DirtyField(ent.Owner, ent.Comp, nameof(TirednessComponent.CurrentTiredness));
        }

        UpdateAlert(ent.Owner, ent.Comp);
    }

    [SubscribeLocalEvent]
    private void OnComponentInit(EntityUid uid, TirednessComponent component, ComponentInit args) =>
        UpdateSleepAction(uid, component);

    [SubscribeLocalEvent]
    private void OnMapInit(EntityUid uid, TirednessComponent component, MapInitEvent args)
    {
        UpdateAlert(uid, component);
        UpdateSleepAction(uid, component);
    }

    [SubscribeLocalEvent]
    private void OnEntityInserted(EntityUid uid, TirednessComponent component, EntGotInsertedIntoContainerMessage args) =>
        UpdateSleepAction(uid, component);

    [SubscribeLocalEvent]
    private void OnEntityRemoved(EntityUid uid, TirednessComponent component, EntGotRemovedFromContainerMessage args) =>
        RemoveSleepAction(uid, component);

    [SubscribeLocalEvent]
    private void OnShutdown(EntityUid uid, TirednessComponent component, ComponentShutdown args)
    {
        _alerts.ClearAlertCategory(uid, _alertCategory);
        _sleepinessApplicationFailures.Remove(uid);

        if (!TerminatingOrDeleted(uid) &&
            component.SleepAction is { } action &&
            TryComp<ActionComponent>(action, out _))
        {
            _actionContainers.RemoveAction(action);
            QueueDel(action);
        }
    }

    [SubscribeLocalEvent]
    private void OnRejuvenate(EntityUid uid, TirednessComponent component, RejuvenateEvent args)
    {
        component.CurrentTiredness = TimeSpan.Zero;
        DirtyField(uid, component, nameof(TirednessComponent.CurrentTiredness));
        _alerts.ClearAlertCategory(uid, _alertCategory);
    }

    private void UpdateAlert(EntityUid uid, TirednessComponent component)
    {
        if (component.CurrentTiredness < _alertThreshold)
        {
            _alerts.ClearAlertCategory(uid, _alertCategory);
            return;
        }

        var severity = component.CurrentTiredness >= MaximumTiredness
            ? (short)2
            : component.CurrentTiredness >= _severeAlertThreshold
                ? (short)1
                : (short)0;
        _alerts.ShowAlert(uid, _alert, severity);
    }

    private void UpdateSleepAction(EntityUid uid, TirednessComponent component)
    {
        if (!_containers.IsEntityInContainer(uid))
        {
            RemoveSleepAction(uid, component);
            return;
        }

        if (component.SleepAction is { } action &&
            TryComp<ActionComponent>(action, out var actionComponent) &&
            actionComponent.AttachedEntity == uid)
        {
            return;
        }

        var oldAction = component.SleepAction;
        if (!_actions.AddAction(uid, ref component.SleepAction, SleepingSystem.SleepActionId, uid))
        {
            Log.Error($"Failed to add sleep action for tired entity {uid} inside a container.");
            return;
        }

        if (oldAction != component.SleepAction)
            DirtyField(uid, component, nameof(TirednessComponent.SleepAction));
    }

    private void RemoveSleepAction(EntityUid uid, TirednessComponent component)
    {
        if (component.SleepAction is not { } action ||
            !TryComp<ActionComponent>(action, out var actionComponent) ||
            actionComponent.AttachedEntity != uid)
        {
            return;
        }

        _actions.RemoveAction(uid, component.SleepAction);
    }
}
