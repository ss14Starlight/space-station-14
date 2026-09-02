using System.Numerics;
using Content.Server.Actions;
using Content.Server.CombatMode;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.EntitySystems;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Camera;
using Content.Shared.Charges.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Wieldable;
using Robust.Server.Audio;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
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
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private CombatModeSystem _combatMode = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private SharedWieldableSystem _wieldable = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    // Subtle relative to explosions (which scale up to ~0.4f) - a jolt, not a blast.
    private const float BiteHarderCameraKick = 0.15f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatchComponent, ComponentStartup>(OnLatchStartup);
        SubscribeLocalEvent<LatchComponent, ComponentShutdown>(OnLatchShutdown);
        SubscribeLocalEvent<LatchComponent, DamageChangedEvent>(OnLatcherDamaged);
        SubscribeLocalEvent<LatchComponent, MobStateChangedEvent>(OnLatcherMobStateChanged);

        SubscribeLocalEvent<LatchedComponent, ComponentShutdown>(OnLatchedShutdown);
        SubscribeLocalEvent<LatchedComponent, MobStateChangedEvent>(OnTargetMobStateChanged);

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

        if (TryComp<LatchComponent>(comp.Latcher, out var latchComp) && latchComp.Active && latchComp.Target == uid)
            EndLatch(comp.Latcher, latchComp);
    }

    private void OnBiteHarderAction(LatchBiteHarderActionEvent ev)
    {
        if (ev.Handled)
            return;

        if (!TryComp<LatchComponent>(ev.Performer, out var comp))
            return;

        ev.Handled = DoBiteHarder(ev.Performer, comp);
    }

    /// <summary>
    /// Deals one latch tick early and extends the duration.
    /// </summary>
    private bool DoBiteHarder(EntityUid uid, LatchComponent comp)
    {
        if (!comp.Active || comp.Target is not { } target)
            return false;

        // Ratio of the bite's damage that got through armor.
        var effectiveness = 0f;

        if (!comp.TickPaused)
        {
            var dealt = DealTick(uid, comp, target);
            var attempted = comp.DamagePerTick.GetTotal();
            effectiveness = attempted > FixedPoint2.Zero ? (dealt.GetTotal() / attempted).Float() : 1f;

            _stamina.TakeStaminaDamage(target, comp.StaminaDamagePerBite, source: uid);
        }

        var extended = comp.EndTime + comp.ExtensionPerBite.Multiply(effectiveness);
        comp.EndTime = extended > comp.MaxEndTime ? comp.MaxEndTime : extended;
        Dirty(uid, comp);

        _audio.PlayPvs(comp.BiteHarderSound, uid);
        RaiseNetworkEvent(new LatchBiteShakeEvent(GetNetEntity(uid)), Filter.Pvs(uid, entityManager: EntityManager));

        var kickDirection = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(uid);
        if (kickDirection != Vector2.Zero)
            _recoil.KickCamera(target, kickDirection.Normalized() * BiteHarderCameraKick);

        return true;
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
    /// grants Bite Harder. The joint's already been created by the shared
    /// base class by the time this runs.
    /// </summary>
    /// <param name="target">The entity being latched onto.</param>
    protected override void StartLatch(EntityUid uid, LatchComponent comp, EntityUid target)
    {
        comp.Active = true;
        comp.Target = target;
        comp.EndTime = Timing.CurTime + comp.BaseDuration;
        comp.MaxEndTime = Timing.CurTime + comp.MaxDuration;
        comp.NextTickTime = Timing.CurTime + comp.TickInterval;
        comp.StartTime = Timing.CurTime;
        comp.TickPaused = false;

        var latched = EnsureComp<LatchedComponent>(target);
        latched.Latcher = uid;
        Dirty(target, latched);

        var toLatcher = _transform.GetWorldPosition(uid) - _transform.GetWorldPosition(target);
        var withinObscureRange = MathF.Abs(toLatcher.Y) <= comp.UiObscureNorthRange
            && MathF.Abs(toLatcher.X) <= comp.UiObscureHorizontalTolerance;

        // K9 north of target -> K9 sits where the target's own UI would be -> target's UI flips below.
        comp.TargetUiBelow = withinObscureRange && toLatcher.Y > 0f;

        // K9 south of target -> target sits where the K9's own UI would be -> latcher's UI flips below.
        comp.LatcherUiBelow = withinObscureRange && toLatcher.Y < 0f;

        if (TryComp<PullableComponent>(uid, out var latcherPullable))
            _pulling.TryStopPull(uid, latcherPullable);

        if (TryComp<PullableComponent>(target, out var targetPullable))
            _pulling.TryStopPull(target, targetPullable);

        if (_virtualItem.TrySpawnVirtualItemInHand(uid, target, out var blockingItem))
        {
            latched.BlockedHandItem = blockingItem;
            EnsureComp<LatchBlockedHandComponent>(blockingItem.Value);
        }

        _wieldable.UnwieldAll(target, force: true);

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

        // Refund if the latch ended almost immediately.
        if (comp.Active && comp.ActionEntity is { } actionEnt && Timing.CurTime - comp.StartTime < comp.RefundGracePeriod)
        {
            _charges.AddCharges((actionEnt, null, null), 1);
            _action.ClearCooldown(actionEnt);
        }

        comp.Active = false;
        comp.Target = null;
        comp.TickPaused = false;

        if (comp.LatchJointId is { } jointId)
        {
            _joints.RemoveJoint(uid, jointId);
            comp.LatchJointId = null;
        }

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
    /// Applies one latch damage tick; returns actual damage dealt (post-armor).
    /// </summary>
    private DamageSpecifier DealTick(EntityUid uid, LatchComponent comp, EntityUid target)
    {
        _damageable.TryChangeDamage(target, comp.DamagePerTick, out var dealt, origin: uid);

        if (_random.Prob(comp.ScreamChance))
            _chat.TryEmoteWithoutChat(target, "Scream");

        if (_random.Prob(comp.ScreamChance))
            _chat.TryEmoteWithoutChat(uid, "Snarl");

        return dealt;
    }

    /// <summary>
    /// A hit on the latcher shortens the duration, scaled by damage dealt.
    /// </summary>
    private void OnLatcherDamaged(EntityUid uid, LatchComponent comp, DamageChangedEvent ev)
    {
        if (!comp.Active || !ev.DamageIncreased || ev.DamageDelta is not { } delta)
            return;

        var dealt = delta.GetTotal();
        var scale = comp.ReferenceDamage > FixedPoint2.Zero ? (dealt / comp.ReferenceDamage).Float() : 1f;

        comp.EndTime -= comp.ReductionPerHit.Multiply(scale);
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

            // Knocked out of range; the joint pulls it back, this just times out if it can't.
            var distance = (_transform.GetWorldPosition(uid) - _transform.GetWorldPosition(target)).Length();
            if (distance > comp.DriftBreakRange + comp.DriftBreakTolerance &&
                now - comp.StartTime >= comp.RefundGracePeriod)
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
