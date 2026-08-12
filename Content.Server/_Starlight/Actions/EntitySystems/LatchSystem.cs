using Content.Server.Actions;
using Content.Server.CombatMode;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.EntitySystems;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Actions.EntitySystems;

/// <summary>
/// Handles the latch ability: pinning, bite-harder extension, DoT, and end conditions.
/// </summary>
public sealed partial class LatchSystem : SharedLatchSystem
{
    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private AlertsSystem _alert = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private CombatModeSystem _combatMode = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatchComponent, ComponentStartup>(OnLatchStartup);
        SubscribeLocalEvent<LatchComponent, ComponentShutdown>(OnLatchShutdown);
        SubscribeLocalEvent<LatchComponent, DamageChangedEvent>(OnLatcherDamaged);
        SubscribeLocalEvent<LatchComponent, MobStateChangedEvent>(OnLatcherMobStateChanged);
        SubscribeLocalEvent<LatchComponent, AttackAttemptEvent>(OnLatcherAttackAttempt);

        SubscribeLocalEvent<LatchedComponent, ComponentShutdown>(OnLatchedShutdown);
        SubscribeLocalEvent<LatchedComponent, MobStateChangedEvent>(OnTargetMobStateChanged);

        SubscribeLocalEvent<LatchActionEvent>(OnLatchAction);
        SubscribeLocalEvent<LatchBiteHarderActionEvent>(OnBiteHarderAction);
        SubscribeLocalEvent<LatchReleaseActionEvent>(OnReleaseAction);
    }

    /// <summary>
    /// Grants the latch action on component add.
    /// </summary>
    private void OnLatchStartup(EntityUid uid, LatchComponent comp, ComponentStartup ev)
    {
        _action.AddAction(uid, ref comp.ActionEntity, comp.Action);
    }

    /// <summary>
    /// Cleans up actions; ends an active latch if the component is removed early.
    /// </summary>
    private void OnLatchShutdown(EntityUid uid, LatchComponent comp, ComponentShutdown ev)
    {
        _action.RemoveAction(uid, comp.ActionEntity);
        _action.RemoveAction(uid, comp.BiteHarderActionEntity);
        _action.RemoveAction(uid, comp.ReleaseActionEntity);

        if (comp.Active)
            EndLatch(uid, comp);
    }

    /// <summary>
    /// Frees the blocked hand and restores standing, unless crit/death owns the pose.
    /// </summary>
    private void OnLatchedShutdown(EntityUid uid, LatchedComponent comp, ComponentShutdown ev)
    {
        if (comp.BlockedHandItem is { } item && Exists(item))
        {
            if (TryComp<VirtualItemComponent>(item, out var virtItem))
                _virtualItem.DeleteVirtualItem((item, virtItem), uid);
        }

        if (!_mobState.IsIncapacitated(uid))
            _standing.Stand(uid);
    }

    /// <summary>
    /// Validates and starts a latch attempt.
    /// </summary>
    private void OnLatchAction(LatchActionEvent ev)
    {
        if (ev.Handled)
            return;

        var uid = ev.Performer;
        if (!TryComp<LatchComponent>(uid, out var comp) || comp.Active)
            return;

        var target = ev.Target;
        if (target == uid)
            return;

        if (!_whitelist.IsWhitelistPassOrNull(comp.Whitelist, target))
            return;

        if (HasComp<LatchedComponent>(target))
            return;

        StartLatch(uid, comp, target);
        ev.Handled = true;
    }

    /// <summary>
    /// Blocks manual attacks while latched, so Bite Harder is the only option.
    /// </summary>
    private void OnLatcherAttackAttempt(EntityUid uid, LatchComponent comp, AttackAttemptEvent ev)
    {
        if (comp.Active)
            ev.Cancel();
    }

    private void OnBiteHarderAction(LatchBiteHarderActionEvent ev)
    {
        if (ev.Handled)
            return;

        var uid = ev.Performer;
        if (!TryComp<LatchComponent>(uid, out var comp) || !comp.Active || comp.Target is not { } target)
            return;

        var extended = comp.EndTime + comp.ExtensionPerBite;
        comp.EndTime = extended > comp.MaxEndTime ? comp.MaxEndTime : extended;
        Dirty(uid, comp);

        _audio.PlayPvs(comp.BiteHarderSound, uid);

        if (!comp.TickPaused)
            DealTick(uid, comp, target);

        ev.Handled = true;
    }

    /// <summary>
    /// Lets the latcher voluntarily end an active latch at any time.
    /// </summary>
    private void OnReleaseAction(LatchReleaseActionEvent ev)
    {
        if (ev.Handled)
            return;

        var uid = ev.Performer;
        if (!TryComp<LatchComponent>(uid, out var comp) || !comp.Active)
            return;

        EndLatch(uid, comp);
        ev.Handled = true;
    }

    /// <summary>
    /// Begins a latch: locks movement, downs the target, blocks a hand, and
    /// grants Bite Harder.
    /// </summary>
    /// <param name="target">The entity being latched onto.</param>
    private void StartLatch(EntityUid uid, LatchComponent comp, EntityUid target)
    {
        comp.Active = true;
        comp.Target = target;
        comp.EndTime = Timing.CurTime + comp.BaseDuration;
        comp.MaxEndTime = Timing.CurTime + comp.MaxDuration;
        comp.NextTickTime = Timing.CurTime + comp.TickInterval;
        comp.TickPaused = false;

        var latched = EnsureComp<LatchedComponent>(target);
        latched.Latcher = uid;
        Dirty(target, latched);

        if (_virtualItem.TrySpawnVirtualItemInHand(uid, target, out var blockingItem))
            latched.BlockedHandItem = blockingItem;

        _standing.Down(target, force: true);

        // Re-asserted every tick in Update() too, so it can't be toggled back on.
        _combatMode.SetInCombatMode(uid, false);

        _action.AddAction(uid, ref comp.BiteHarderActionEntity, comp.BiteHarderAction);
        _action.AddAction(uid, ref comp.ReleaseActionEntity, comp.ReleaseAction);

        _speed.RefreshMovementSpeedModifiers(uid);
        _speed.RefreshMovementSpeedModifiers(target);

        _alert.ShowAlert(uid, comp.LatcherAlert);
        _alert.ShowAlert(target, comp.LatchAlert);

        _audio.PlayPvs(comp.LatchStartSound, uid);
        _chat.TryEmoteWithoutChat(uid, "Growl");

        Dirty(uid, comp);
    }

    /// <summary>
    /// Ends the latch. Safe to call on an already-inactive component.
    /// </summary>
    private void EndLatch(EntityUid uid, LatchComponent comp)
    {
        var target = comp.Target;

        comp.Active = false;
        comp.Target = null;
        comp.TickPaused = false;

        _action.RemoveAction(uid, comp.BiteHarderActionEntity);
        comp.BiteHarderActionEntity = null;

        _action.RemoveAction(uid, comp.ReleaseActionEntity);
        comp.ReleaseActionEntity = null;

        _speed.RefreshMovementSpeedModifiers(uid);
        _alert.ClearAlert(uid, comp.LatcherAlert);

        if (target is { } targetUid && Exists(targetUid))
        {
            RemComp<LatchedComponent>(targetUid);
            _alert.ClearAlert(targetUid, comp.LatchAlert);
            _speed.RefreshMovementSpeedModifiers(targetUid);
        }

        Dirty(uid, comp);
    }

    /// <summary>
    /// Applies one instance of latch damage, with a chance to scream/snarl.
    /// </summary>
    private void DealTick(EntityUid uid, LatchComponent comp, EntityUid target)
    {
        _damageable.TryChangeDamage(target, comp.DamagePerTick, origin: uid);

        if (_random.Prob(comp.ScreamChance))
            _chat.TryEmoteWithoutChat(target, "Scream");

        if (_random.Prob(comp.ScreamChance))
            _chat.TryEmoteWithoutChat(uid, "Snarl");
    }

    /// <summary>
    /// A hit on the latcher shortens the remaining duration by a fixed step.
    /// </summary>
    private void OnLatcherDamaged(EntityUid uid, LatchComponent comp, DamageChangedEvent ev)
    {
        if (!comp.Active || !ev.DamageIncreased)
            return;

        comp.EndTime -= comp.ReductionPerHit;
        Dirty(uid, comp);

        if (comp.EndTime <= Timing.CurTime)
            EndLatch(uid, comp);
    }

    /// <summary>
    /// Latcher going critical or dying ends the latch immediately.
    /// </summary>
    private void OnLatcherMobStateChanged(EntityUid uid, LatchComponent comp, ref MobStateChangedEvent ev)
    {
        if (!comp.Active)
            return;

        if (ev.NewMobState is MobState.Critical or MobState.Dead)
            EndLatch(uid, comp);
    }

    /// <summary>
    /// Target death ends the latch; target crit only pauses the DoT.
    /// </summary>
    /// <remarks>
    /// The pin stays active through crit, so reviving out of crit mid-latch
    /// doesn't free the target.
    /// </remarks>
    private void OnTargetMobStateChanged(EntityUid uid, LatchedComponent comp, ref MobStateChangedEvent ev)
    {
        if (!TryComp<LatchComponent>(comp.Latcher, out var latchComp) || !latchComp.Active)
            return;

        if (ev.NewMobState is MobState.Dead)
        {
            EndLatch(comp.Latcher, latchComp);
            return;
        }

        // Incapacitated (crit): pause damage, keep the pin active.
        latchComp.TickPaused = ev.NewMobState == MobState.Critical;
    }

    /// <summary>
    /// Per-tick upkeep: end conditions, DoT ticks, combat-mode enforcement.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<LatchComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active)
                continue;

            // Stunned/slept latcher ends the latch immediately.
            if (HasComp<StunnedComponent>(uid) ||
                HasComp<SleepingComponent>(uid) ||
                _mobState.IsIncapacitated(uid))
            {
                EndLatch(uid, comp);
                continue;
            }

            if (now >= comp.EndTime)
            {
                EndLatch(uid, comp);
                continue;
            }

            if (comp.Target is not { } target || !Exists(target))
            {
                EndLatch(uid, comp);
                continue;
            }

            // Re-assert every tick so this can't be toggled back on mid-latch.
            _combatMode.SetInCombatMode(uid, false);

            if (!comp.TickPaused && now >= comp.NextTickTime)
            {
                DealTick(uid, comp, target);
                comp.NextTickTime = now + comp.TickInterval;
            }
        }
    }
}
