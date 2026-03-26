using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Database;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared._Starlight.Cybernetics.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Actions;
using Content.Shared.Humanoid;
using Content.Shared.Projectiles;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Cybernetics;

public abstract partial class SharedCyberneticDisruptionSystem : EntitySystem
{
    public static readonly EntProtoId DisruptionId = "StatusEffectCyberneticDisruption";

    [Dependency] protected readonly IGameTiming GameTiming = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] protected readonly AlertsSystem Alerts = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CyberneticDisruptionComponent, ComponentStartup>(UpdateCybernetics);
        SubscribeLocalEvent<CyberneticDisruptionComponent, ComponentShutdown>(OnCyberneticDisruptionShutdown);

        // New Status Effect subscriptions
        SubscribeLocalEvent<CyberneticDisruptionStatusEffectComponent, StatusEffectAppliedEvent>(OnCyberneticDisruptionStatusApplied);
        SubscribeLocalEvent<CyberneticDisruptionStatusEffectComponent, StatusEffectRemovedEvent>(OnCyberneticDisruptionStatusRemoved);
        SubscribeLocalEvent<CyberneticDisruptionStatusEffectComponent, StatusEffectRelayedEvent<CyberneticDisruptionEndAttemptEvent>>(OnCyberneticDisruptionEndAttempt);

        SubscribeLocalEvent<CyberneticDisruptionOnCollideComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnCyberneticDisruptionShutdown(Entity<CyberneticDisruptionComponent> ent, ref ComponentShutdown args) => UpdateCybernetics(ent, ent.Comp, args);

    private void UpdateCybernetics(EntityUid uid, CyberneticDisruptionComponent component, EntityEventArgs args)
    {
        var ev = new CyberneticDisruptionEvent(uid);
        foreach (var part in _bodySystem.GetBodyChildren(uid))
            RaiseLocalEvent(part.Id, ref ev);
        foreach (var part in _bodySystem.GetBodyOrgans(uid))
            RaiseLocalEvent(part.Id, ref ev);
        foreach (var action in _actions.GetActions(uid))
            RaiseLocalEvent(action.Owner, ref ev);
    }

    public bool TryAddCyberneticDisruptionDuration(EntityUid uid, TimeSpan duration, bool refreshDuration = false)
    {
        if (refreshDuration && !_status.TrySetStatusEffectDuration(uid, DisruptionId, duration))
            return false;

        if (!refreshDuration && !_status.TryAddStatusEffectDuration(uid, DisruptionId, duration))
            return false;

        OnCyberneticDisruptionSuccessful(uid, duration);
        return true;
    }

    public bool TryUpdateCyberneticDisruptionDuration(EntityUid uid, TimeSpan? duration)
    {
        if (!_status.TryUpdateStatusEffectDuration(uid, DisruptionId, duration))
            return false;

        OnCyberneticDisruptionSuccessful(uid, duration);
        return true;
    }

    private void OnCyberneticDisruptionSuccessful(EntityUid uid, TimeSpan? duration)
    {
        var ev = new CyberneticDisruptionEvent(uid);
        foreach (var part in _bodySystem.GetBodyChildren(uid))
            RaiseLocalEvent(part.Id, ref ev);
        foreach (var part in _bodySystem.GetBodyOrgans(uid))
            RaiseLocalEvent(part.Id, ref ev);
        foreach (var action in _actions.GetActions(uid))
            RaiseLocalEvent(action.Owner, ref ev);

        var timeForLogs = duration.HasValue
            ? duration.Value.Seconds.ToString()
            : "Infinite";
        _adminLogger.Add(LogType.EntityEffect, LogImpact.Low, $"{ToPrettyString(uid):user} disrupted for {timeForLogs} seconds");
    }

    public bool TryRemoveCyberneticDisruption(Entity<CyberneticDisruptionComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, logMissing: false))
            return true;

        var ev = new CyberneticDisruptionEndAttemptEvent();
        RaiseLocalEvent(entity, ref ev);

        return !ev.Cancelled && RemComp<CyberneticDisruptionComponent>(entity);
    }

    private void OnCyberneticDisruptionStatusApplied(Entity<CyberneticDisruptionStatusEffectComponent> entity, ref StatusEffectAppliedEvent args)
    {
        if (GameTiming.ApplyingState)
            return;

        EnsureComp<CyberneticDisruptionComponent>(args.Target);
    }

    private void OnCyberneticDisruptionStatusRemoved(Entity<CyberneticDisruptionStatusEffectComponent> entity, ref StatusEffectRemovedEvent args) => TryRemoveCyberneticDisruption(args.Target);

    private void OnCyberneticDisruptionEndAttempt(Entity<CyberneticDisruptionStatusEffectComponent> entity, ref StatusEffectRelayedEvent<CyberneticDisruptionEndAttemptEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        var ev = args.Args;
        ev.Cancelled = true;
        args.Args = ev;
    }

    private void OnProjectileHit(EntityUid uid, CyberneticDisruptionOnCollideComponent component, ref ProjectileHitEvent args)
    => OnCollide(uid, component, args.Target);

    private void OnCollide(EntityUid uid, CyberneticDisruptionOnCollideComponent component, EntityUid target)
    {
        // you can't disrupt cybernetics on things which cannot have cybernetics in the first place
        if (!HasComp<HumanoidAppearanceComponent>(target))
            return;

        if(_random.NextFloat() <= component.DisableChance)
            TryAddCyberneticDisruptionDuration(target, component.Duration);
    }
}
