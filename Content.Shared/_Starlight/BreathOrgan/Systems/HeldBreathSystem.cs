using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Database;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared._Starlight.BreathOrgan.Components;

namespace Content.Shared._Starlight.BreathOrgan;

public sealed partial class HeldBreathSystem : EntitySystem
{
    public static readonly EntProtoId HeldBreathId = "StatusEffectHeldBreath";

    [Dependency] protected readonly IGameTiming GameTiming = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] protected readonly AlertsSystem Alerts = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        // New Status Effect subscriptions
        SubscribeLocalEvent<HeldBreathStatusEffectComponent, StatusEffectAppliedEvent>(OnHeldBreathStatusApplied);
        SubscribeLocalEvent<HeldBreathStatusEffectComponent, StatusEffectRemovedEvent>(OnHeldBreathStatusRemoved);
        SubscribeLocalEvent<HeldBreathStatusEffectComponent, StatusEffectRelayedEvent<HeldBreathEndAttemptEvent>>(OnHeldBreathEndAttempt);
        SubscribeLocalEvent<HeldBreathComponent, HeldBreathAlertEvent>(OnClickAlert);
    }

    public bool TryAddHeldBreathDuration(EntityUid uid, TimeSpan duration, bool refreshDuration = false)
    {
        if (refreshDuration && !_status.TrySetStatusEffectDuration(uid, HeldBreathId, duration))
            return false;

        if (!refreshDuration && !_status.TryAddStatusEffectDuration(uid, HeldBreathId, duration))
            return false;

        OnHeldBreathSuccessful(uid, duration);
        return true;
    }

    public bool TryUpdateHeldBreathDuration(EntityUid uid, TimeSpan? duration)
    {
        if (!_status.TryUpdateStatusEffectDuration(uid, HeldBreathId, duration))
            return false;

        OnHeldBreathSuccessful(uid, duration);
        return true;
    }

    private void OnHeldBreathSuccessful(EntityUid uid, TimeSpan? duration)
    {
        var timeForLogs = duration.HasValue
            ? duration.Value.Seconds.ToString()
            : "Infinite";
        _adminLogger.Add(LogType.EntityEffect, LogImpact.Low, $"{ToPrettyString(uid):user} disrupted for {timeForLogs} seconds");
    }

    public bool TryRemoveHeldBreath(Entity<HeldBreathComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, logMissing: false))
            return true;

        var ev = new HeldBreathEndAttemptEvent();
        RaiseLocalEvent(entity, ref ev);

        return !ev.Cancelled && RemComp<HeldBreathComponent>(entity);
    }

    private void OnHeldBreathStatusApplied(Entity<HeldBreathStatusEffectComponent> entity, ref StatusEffectAppliedEvent args)
    {
        if (GameTiming.ApplyingState)
            return;

        EnsureComp<HeldBreathComponent>(args.Target);
    }

    private void OnHeldBreathStatusRemoved(Entity<HeldBreathStatusEffectComponent> entity, ref StatusEffectRemovedEvent args) => TryRemoveHeldBreath(args.Target);

    private void OnHeldBreathEndAttempt(Entity<HeldBreathStatusEffectComponent> entity, ref StatusEffectRelayedEvent<HeldBreathEndAttemptEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        var ev = args.Args;
        ev.Cancelled = true;
        args.Args = ev;
    }
    private void OnClickAlert(Entity<HeldBreathComponent> entity, ref HeldBreathAlertEvent args)
    {
        _status.TryRemoveStatusEffect(entity.Owner, HeldBreathId);
        TryRemoveHeldBreath(entity.Owner);
    }
}
