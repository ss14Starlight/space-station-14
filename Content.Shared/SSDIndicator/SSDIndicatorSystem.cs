using Content.Shared.CCVar;
using Content.Shared.NPC; // Starlight
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

// Starlight-start
using Content.Shared.Bed.Sleep;
using Content.Shared._Starlight.SSDIndicator.Events;
using Content.Shared.DoAfter;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Starlight.CryoTeleportation;
// Starlight-end

namespace Content.Shared.SSDIndicator;

/// <summary>
///     Handle changing player SSD indicator status
/// </summary>
public sealed class SSDIndicatorSystem : EntitySystem
{
    public static readonly EntProtoId StatusEffectSSDSleeping = "StatusEffectSSDSleeping";
    private static readonly TimeSpan SsdDoAfterDelay = TimeSpan.FromSeconds(10);

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SleepingSystem _sleep = default!; // Starlight
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!; // Starlight

    private bool _icSsdSleep;
    private float _icSsdSleepTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<SSDIndicatorComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SSDIndicatorComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<SSDIndicatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SSDIndicatorComponent, MoveInputEvent>(OnMoveInput); // Starlight
        SubscribeLocalEvent<SSDIndicatorComponent, WakeActionEvent>(OnWakeAction); // Starlight
        SubscribeLocalEvent<SSDIndicatorComponent, SSDTryDoAfterEvent>(OnSSDTry); // Starlight

        _cfg.OnValueChanged(CCVars.ICSSDSleep, obj => _icSsdSleep = obj, true);
        _cfg.OnValueChanged(CCVars.ICSSDSleepTime, obj => _icSsdSleepTime = obj, true);
    }

    // Starlight start
    private void OnPlayerAttached(EntityUid uid, SSDIndicatorComponent component, PlayerAttachedEvent args) => TryRemoveSSD(uid, component);

    // Avoid marking temporary mind transfer shells as SSD. (Wizard Jaunt Spell, Rod Spell, Golden Mask, etc.)
    // Real disconnects keep mind on body and still go through SSD.
    private void OnPlayerDetached(EntityUid uid, SSDIndicatorComponent component, PlayerDetachedEvent args)
    {
        if (TryComp<MindContainerComponent>(uid, out var mindContainer)
            && !mindContainer.HasMind)
            return;

        TrySSD(uid, component, force: true);
    }

    private void OnMoveInput(EntityUid uid, SSDIndicatorComponent comp, MoveInputEvent args) => TryRemoveSSD(uid, comp);

    private void OnWakeAction(EntityUid uid, SSDIndicatorComponent comp, WakeActionEvent args) => TryRemoveSSD(uid, comp);
    // Starlight end (for now :P)

    // Prevents mapped mobs to go to sleep immediately
    private void OnMapInit(EntityUid uid, SSDIndicatorComponent component, MapInitEvent args)
    {
        if (!_icSsdSleep || !component.IsSSD)
            return;

        component.FallAsleepTime = _timing.CurTime + TimeSpan.FromSeconds(_icSsdSleepTime);
        component.NextUpdate = component.FallAsleepTime; // Starlight: schedule the first update at FallAsleepTime, not repeatedly before sleep is even eligible.
        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_icSsdSleep)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SSDIndicatorComponent>();

        while (query.MoveNext(out var uid, out var ssd))
        {
            // Forces the entity to sleep when the time has come
            if (!ssd.IsSSD
                || HasComp<ActiveNPCComponent>(uid) // Starlight
                || ssd.NextUpdate > curTime
                || ssd.FallAsleepTime > curTime
                || TerminatingOrDeleted(uid))
                continue;

            _statusEffects.TryUpdateStatusEffectDuration(uid, StatusEffectSSDSleeping);
            ssd.NextUpdate = curTime + ssd.UpdateInterval; // Starlight: advance from current time instead of incrementing by UpdateInterval.
            Dirty(uid, ssd);
        }
    }

    #region Starlight

    // Starlight-start
    // /ssd uses a doAfter. After completion, apply SSD with immediate sleep timing.
    private void OnSSDTry(EntityUid uid, SSDIndicatorComponent component, SSDTryDoAfterEvent args) => SSD(uid, component, sleepDelayOverride: TimeSpan.Zero);
    // Starlight-end

    /// <summary>
    /// Attempts to set the entity as SSD.
    /// </summary>
    /// <param name="force">bypasses doAfter.</param>
    /// <returns>True if succesful</returns>
    public bool TrySSD(EntityUid uid, SSDIndicatorComponent? comp, bool force = false)
    {
        if (!Resolve(uid, ref comp) 
            || comp.IsSSD 
            || TerminatingOrDeleted(uid))
            return false;

        if (!force)
        {
            var doAfter = new DoAfterArgs(EntityManager, uid, SsdDoAfterDelay, new SSDTryDoAfterEvent(), uid) // Starlight: make doAfter delay as a named constant for clarity.
            {
                BreakOnMove = true,
            };
            _doAfter.TryStartDoAfter(doAfter);
        }
        else
            SSD(uid, comp);

        return true;
    }

    // Starlight-start
    private void SSD(EntityUid uid, SSDIndicatorComponent component, TimeSpan? sleepDelayOverride = null)
    {
        component.IsSSD = true;

        if (_icSsdSleep)
        {
            // If sleepDelayOverride is provided, use that instead of the config value. This allows /ssd to apply SSD immediately without waiting for the usual delay.
            var sleepDelay = sleepDelayOverride ?? TimeSpan.FromSeconds(_icSsdSleepTime);
            component.FallAsleepTime = _timing.CurTime + sleepDelay;
            component.NextUpdate = component.FallAsleepTime; // same reason as OnMapInit, first check should happen when sleep can apply.
        }
        // Starlight-end

        Dirty(uid, component);
    }

    /// <summary>
    /// Attempts to remove the SSD condition from the entity.
    /// </summary>
    /// <returns>True if succesful</returns>
    public bool TryRemoveSSD(EntityUid uid, SSDIndicatorComponent? comp)
    {
        if (!Resolve(uid, ref comp)
            || !comp.IsSSD 
            || TerminatingOrDeleted(uid))
            return false;

        comp.IsSSD = false;

        if (_icSsdSleep)
        {
            comp.FallAsleepTime = TimeSpan.Zero;
            _statusEffects.TryRemoveStatusEffect(uid, StatusEffectSSDSleeping);
            _sleep.TryWaking(uid, force: true); // Starlight: force waking up after removing ssd.
        }

        if (TryComp<TargetCryoTeleportationComponent>(uid, out var cryoTeleport) && cryoTeleport.TimeDelay > TimeSpan.FromSeconds(0))
            cryoTeleport.TimeDelay = TimeSpan.FromSeconds(0); // Reset time delay to 0.

        Dirty(uid, comp);
        return true;
    }
    
    #endregion
}
