using System.Collections.Generic;
using Content.Shared.Alert;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared._Starlight.Tiredness.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Tiredness.Systems;

public abstract partial class SharedTirednessSystem : EntitySystem
{
    private const float UpdateInterval = 1f;

    public static readonly TimeSpan MaximumTiredness = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan _alertThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan _severeAlertThreshold = TimeSpan.FromMinutes(25);
    private static readonly ProtoId<AlertCategoryPrototype> _alertCategory = "Tiredness";
    private static readonly ProtoId<AlertPrototype> _alert = "FelionoidTiredness";

    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private ActionContainerSystem _actionContainers = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private readonly HashSet<EntityUid> _pendingMovementSpeedRefreshes = new();
    private float _updateAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        RefreshPendingMovementSpeedModifiers();

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
            var oldMovementSpeedStage = GetTirednessMovementSpeedStage(oldTirednessSeconds);
            var isSleeping = HasComp<SleepingComponent>(uid);
            var tirednessDelta = isSleeping ? -updateTime * tiredness.SleepRecoveryMultiplier : updateTime;
            var newTirednessSeconds = Math.Clamp(oldTirednessSeconds + tirednessDelta, 0, maximumTirednessSeconds);
            if (newTirednessSeconds != oldTirednessSeconds)
            {
                tiredness.CurrentTiredness = TimeSpan.FromSeconds(newTirednessSeconds);
                DirtyField(uid, tiredness, nameof(TirednessComponent.CurrentTiredness));
            }

            if (oldMovementSpeedStage != GetTirednessMovementSpeedStage(newTirednessSeconds))
                _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);

            UpdateAlert(uid, tiredness);
        }
    }

    /// <summary>
    /// Sets an entity's tiredness duration.
    /// </summary>
    /// <param name="ent">The entity whose tiredness to set.</param>
    /// <param name="tiredness">The requested duration, clamped from zero to 30 minutes.</param>
    /// <remarks>
    /// Updates the tiredness alert and refreshes movement speed modifiers if the movement-speed stage changes.
    /// </remarks>
    public void SetTiredness(Entity<TirednessComponent?> ent, TimeSpan tiredness)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var oldMovementSpeedStage = GetTirednessMovementSpeedStage(ent.Comp.CurrentTiredness.TotalSeconds);
        var newTiredness = TimeSpan.FromSeconds(Math.Clamp(tiredness.TotalSeconds, 0, MaximumTiredness.TotalSeconds));
        if (newTiredness != ent.Comp.CurrentTiredness)
        {
            ent.Comp.CurrentTiredness = newTiredness;
            DirtyField(ent.Owner, ent.Comp, nameof(TirednessComponent.CurrentTiredness));

            if (oldMovementSpeedStage != GetTirednessMovementSpeedStage(newTiredness.TotalSeconds))
                _movementSpeedModifier.RefreshMovementSpeedModifiers(ent.Owner);
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
        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
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

        if (!TerminatingOrDeleted(uid))
            _pendingMovementSpeedRefreshes.Add(uid);

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
        var oldMovementSpeedStage = GetTirednessMovementSpeedStage(component.CurrentTiredness.TotalSeconds);
        component.CurrentTiredness = TimeSpan.Zero;
        DirtyField(uid, component, nameof(TirednessComponent.CurrentTiredness));
        _alerts.ClearAlertCategory(uid, _alertCategory);

        if (oldMovementSpeedStage != 0)
            _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
    }

    [SubscribeLocalEvent]
    private void OnRefreshMovementSpeedModifiers(Entity<TirednessComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args) =>
        args.ModifySpeed(GetTirednessMovementSpeedModifier(ent.Comp));

    private void RefreshPendingMovementSpeedModifiers()
    {
        foreach (var uid in _pendingMovementSpeedRefreshes)
        {
            if (!TerminatingOrDeleted(uid))
                _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
        }

        _pendingMovementSpeedRefreshes.Clear();
    }

    private static float GetTirednessMovementSpeedModifier(TirednessComponent tiredness) =>
        GetTirednessMovementSpeedStage(tiredness.CurrentTiredness.TotalSeconds) switch
        {
            2 => tiredness.MaximumTirednessMovementSpeedModifier,
            1 => tiredness.SevereTirednessMovementSpeedModifier,
            _ => 1f
        };

    private static int GetTirednessMovementSpeedStage(double tirednessSeconds) =>
        tirednessSeconds >= MaximumTiredness.TotalSeconds ? 2
            : tirednessSeconds >= _severeAlertThreshold.TotalSeconds ? 1
            : 0;

    private void UpdateAlert(EntityUid uid, TirednessComponent component)
    {
        if (component.CurrentTiredness < _alertThreshold)
        {
            _alerts.ClearAlertCategory(uid, _alertCategory);
            return;
        }

        var severity = component.CurrentTiredness >= MaximumTiredness
            ? (short) 2
            : component.CurrentTiredness >= _severeAlertThreshold
                ? (short) 1
                : (short) 0;
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
