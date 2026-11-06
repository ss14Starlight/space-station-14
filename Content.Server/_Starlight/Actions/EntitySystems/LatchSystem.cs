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
using Content.Shared.Damage.Components;
using Content.Shared.Gravity;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Random;

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
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private EntityWhitelistSystem _entityWhitelist = default!;

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

        SubscribeNetworkEvent<LatchStruggleRequestEvent>(OnStruggleRequest);
    }

    /// <summary>
    /// Grants the latch action on component add.
    /// </summary>
    private void OnLatchStartup(EntityUid uid, LatchComponent comp, ComponentStartup ev)
        => _action.AddAction(uid, ref comp.ActionEntity, comp.Action);

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
    /// Restores standing, unless crit/death owns the pose.
    /// </summary>
    private void OnLatchedShutdown(EntityUid uid, LatchedComponent comp, ComponentShutdown ev)
    {
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

        // Can't bite a pinned target through a wall. ObstructedSince is only
        // refreshed once per tick in Update, so recheck sight here as well.
        if (comp.ObstructedSince != null || (IsPinnedTarget(target) && !HasLatchLineOfSight(uid, target)))
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

        if (TryComp<LatchStruggleComponent>(target, out var struggle))
        {
            var now = Timing.CurTime;
            struggle.FrenzyEndTime = now + comp.StruggleFrenzyDuration;

            // While paused, the next attempt picks the speed up when it starts.
            if (struggle.Block == LatchStruggleBlock.None)
                RebaseStruggle(struggle, now, GetStruggleSpeed(comp, struggle, now));

            Dirty(target, struggle);
        }

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
        comp.ObstructedSince = null;
        comp.TickPaused = _mobState.IsCritical(target) || _mobState.IsSoftCritical(target);

        // Slow targets in SlowPrototypes; pin everyone else.
        var slowed = IsSlowedTarget(comp, target);

        var latched = EnsureComp<LatchedComponent>(target);
        latched.Latcher = uid;
        latched.SpeedMultiplier = slowed ? comp.SlowSpeedMultiplier : 0f;
        Dirty(target, latched);

        comp.LatcherWeightless = IsFloatingTarget(target);

        // Only some targets get the struggle minigame; everything else about the latch is the same.
        if (_entityWhitelist.IsWhitelistPassOrNull(comp.StruggleWhitelist, target))
        {
            var struggle = EnsureComp<LatchStruggleComponent>(target);
            struggle.Block = LatchStruggleBlock.None;
            struggle.LastResult = LatchStruggleResult.None;
            struggle.FrenzyEndTime = TimeSpan.Zero;
            StartStruggleAttempt(comp, struggle, Timing.CurTime + comp.StruggleCooldown, Timing.CurTime);
            Dirty(target, struggle);
        }

        if (TryComp<PullableComponent>(uid, out var latcherPullable))
            _pulling.TryStopPull(uid, latcherPullable);

        if (TryComp<PullableComponent>(target, out var targetPullable))
            _pulling.TryStopPull(target, targetPullable);

        if (!slowed)
            _standing.Down(target, force: true);

        // Re-asserted every tick in Update() too, so it can't be toggled back on.
        _combatMode.SetInCombatMode(uid, false);

        _action.AddAction(uid, ref comp.BiteHarderActionEntity, comp.BiteHarderAction);
        _action.AddAction(uid, ref comp.ReleaseActionEntity, comp.ReleaseAction);

        _speed.RefreshMovementSpeedModifiers(uid);
        _speed.RefreshMovementSpeedModifiers(target);
        _speed.RefreshWeightlessModifiers(uid);
        _speed.RefreshWeightlessModifiers(target);
        _gravity.RefreshWeightless(uid);

        _alert.ShowAlert(uid, comp.LatcherAlert);
        _alert.ShowAlert(target, comp.LatchAlert);

        _audio.PlayPvs(comp.LatchStartSound, uid);
        _chat.TryEmoteWithoutChat(uid, "Growl");

        Dirty(uid, comp);
    }

    /// <inheritdoc/>
    protected override void BreakLatch(Entity<LatchComponent> latcher)
        => EndLatch(latcher.Owner, latcher.Comp);

    /// <summary>
    /// True if the target floats: InAir and able to move in air, like carp,
    /// dragons, and colossi, regardless of gravity.
    /// </summary>
    private bool IsFloatingTarget(EntityUid target) =>
        TryComp<PhysicsComponent>(target, out var body)
            && body.BodyStatus == BodyStatus.InAir
            && HasComp<CanMoveInAirComponent>(target);

    /// <summary>
    /// True if the target's prototype or any parent, abstract included, is in
    /// <see cref="LatchComponent.SlowPrototypes"/>.
    /// </summary>
    private bool IsSlowedTarget(LatchComponent comp, EntityUid target)
    {
        if (comp.SlowPrototypes.Count == 0 || MetaData(target).EntityPrototype is not { } proto)
            return false;

        foreach (var (id, _) in _prototype.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true))
        {
            if (comp.SlowPrototypes.Contains(new EntProtoId(id)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Ends the latch. Safe to call on an already-inactive component.
    /// </summary>
    /// <param name="forceRefund">Refund the charge even outside the usual grace period.</param>
    private void EndLatch(EntityUid uid, LatchComponent comp, bool forceRefund = false)
    {
        var target = comp.Target;

        // Refund if the latch ended almost immediately.
        if (comp.Active && comp.ActionEntity is { } actionEnt &&
            (forceRefund || Timing.CurTime - comp.StartTime < comp.RefundGracePeriod))
        {
            _charges.AddCharges((actionEnt, null, null), 1);
            _action.ClearCooldown(actionEnt);
        }

        comp.Active = false;
        comp.Target = null;
        comp.TickPaused = false;
        comp.ObstructedSince = null;
        comp.LatcherWeightless = false;

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
        _speed.RefreshWeightlessModifiers(uid);
        _gravity.RefreshWeightless(uid);
        _alert.ClearAlert(uid, comp.LatcherAlert);

        if (target is { } targetUid && Exists(targetUid))
        {
            RemComp<LatchStruggleComponent>(targetUid);
            RemComp<LatchedComponent>(targetUid);
            _alert.ClearAlert(targetUid, comp.LatchAlert);
            _speed.RefreshMovementSpeedModifiers(targetUid);
            _speed.RefreshWeightlessModifiers(targetUid);
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

        if (ev.NewMobState is not MobState.Alive)
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
        latchComp.TickPaused = ev.NewMobState is MobState.Critical or MobState.SoftCritical;
    }

    /// <summary>
    /// A latch target pressed Struggle: grade it, shorten the latch, and queue the next attempt.
    /// </summary>
    private void OnStruggleRequest(LatchStruggleRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } target ||
            !TryComp<LatchedComponent>(target, out var latched) ||
            !TryComp<LatchStruggleComponent>(target, out var struggle) ||
            !TryComp<LatchComponent>(latched.Latcher, out var latch) ||
            !latch.Active ||
            latch.Target != target)
        {
            return;
        }

        var now = Timing.CurTime;

        // Paused, or still in the gap after the last press: one press per attempt.
        if (struggle.Block != LatchStruggleBlock.None || now < struggle.SegmentStart)
            return;

        // The client's frame sits up to one tick past its stamped tick; honour that, no further.
        // Clamp passes NaN through, and TimeSpan throws on it.
        var tickOffset = float.IsFinite(msg.TickOffset) ? msg.TickOffset : 0f;
        var maxOffset = (float) Timing.TickPeriod.TotalSeconds;
        var offset = TimeSpan.FromSeconds(Math.Clamp(tickOffset, 0f, maxOffset));
        var cursor = GetStruggleCursor(struggle, now + offset);
        var result = GradeStruggle(cursor, struggle.ZoneCenter, latch.StrugglePerfectWidth, latch.StruggleGoodWidth);

        struggle.LastResult = result;
        struggle.LastPressPosition = cursor;
        struggle.LastZoneCenter = struggle.ZoneCenter;
        struggle.LastPressTime = now;
        StartStruggleAttempt(latch, struggle, now + latch.StruggleCooldown, now);
        Dirty(target, struggle);

        var reduction = result switch
        {
            LatchStruggleResult.Perfect => latch.StrugglePerfectReduction,
            LatchStruggleResult.Good => latch.StruggleGoodReduction,
            _ => TimeSpan.Zero,
        };

        if (reduction <= TimeSpan.Zero)
            return;

        latch.EndTime -= reduction;
        latch.MaxEndTime -= reduction;
        Dirty(latched.Latcher, latch);

        if (latch.EndTime <= now)
            EndLatch(latched.Latcher, latch);
    }

    /// <summary>
    /// Pauses struggling while the target can't act, resumes with a fresh
    /// attempt afterwards, and drops the Bite Harder speed-up once it expires.
    /// </summary>
    private void UpdateStruggle(EntityUid target, LatchStruggleComponent struggle, LatchComponent latch, TimeSpan now)
    {
        var block = GetStruggleBlock(target);
        if (block != struggle.Block)
        {
            if (block == LatchStruggleBlock.None)
            {
                StartStruggleAttempt(latch, struggle, now + latch.StruggleCooldown, now);
            }
            else if (struggle.Block == LatchStruggleBlock.None)
            {
                // Freeze the cursor where it is.
                RebaseStruggle(struggle, now, struggle.Speed);
                struggle.SegmentStart = TimeSpan.MaxValue;
            }

            struggle.Block = block;
            Dirty(target, struggle);
            return;
        }

        if (block != LatchStruggleBlock.None)
            return;

        var speed = GetStruggleSpeed(latch, struggle, now);
        if (MathF.Abs(struggle.Speed - speed) > 0.0001f)
        {
            RebaseStruggle(struggle, now, speed);
            Dirty(target, struggle);
        }
    }

    private LatchStruggleBlock GetStruggleBlock(EntityUid target)
    {
        if (_mobState.IsIncapacitated(target) || HasComp<SleepingComponent>(target))
            return LatchStruggleBlock.Incapacitated;

        if (TryComp<StaminaComponent>(target, out var stamina) && stamina.Critical)
            return LatchStruggleBlock.Exhausted;

        return HasComp<StunnedComponent>(target)
            ? LatchStruggleBlock.Stunned
            : LatchStruggleBlock.None;
    }

    /// <summary>
    /// Picks a new random zone and parks the cursor at the left edge until <paramref name="start"/>.
    /// </summary>
    private void StartStruggleAttempt(LatchComponent latch, LatchStruggleComponent struggle, TimeSpan start, TimeSpan now)
    {
        var edge = (latch.StrugglePerfectWidth / 2f) + latch.StruggleGoodWidth;
        var min = MathF.Max(latch.StruggleMinZoneCenter, edge);
        var max = 1f - edge;

        struggle.ZoneCenter = max > min ? _random.NextFloat(min, max) : 0.5f;
        struggle.SegmentPosition = 0f;
        struggle.SegmentStart = start;
        struggle.Speed = GetStruggleSpeed(latch, struggle, now);
    }

    private static float GetStruggleSpeed(LatchComponent latch, LatchStruggleComponent struggle, TimeSpan now)
    {
        var speed = latch.StruggleCrossingTime > 0f ? 1f / latch.StruggleCrossingTime : 1f;
        return now < struggle.FrenzyEndTime ? speed * latch.StruggleFrenzySpeedMultiplier : speed;
    }

    /// <summary>
    /// Changes cursor speed mid-sweep without the cursor jumping.
    /// </summary>
    private static void RebaseStruggle(LatchStruggleComponent struggle, TimeSpan now, float speed)
    {
        if (now > struggle.SegmentStart)
        {
            struggle.SegmentPosition = GetStruggleUnfolded(struggle, now);
            struggle.SegmentStart = now;
        }

        struggle.Speed = speed;
    }

    /// <summary>
    /// True if the target is held in place rather than slowed. Only pinned
    /// targets are subject to the latch's line-of-sight rules.
    /// </summary>
    private bool IsPinnedTarget(EntityUid target)
        => !TryComp<LatchedComponent>(target, out var latched) || latched.SpeedMultiplier <= 0f;

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
                _mobState.IsDead(uid))
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

            // Follow the target if it starts or stops floating mid-latch.
            var targetFloating = IsFloatingTarget(target);
            if (comp.LatcherWeightless != targetFloating)
            {
                comp.LatcherWeightless = targetFloating;
                _gravity.RefreshWeightless(uid);
                Dirty(uid, comp);
            }

            // Knocked out of range; the joint pulls it back, this just times out if it can't.
            var distance = (_transform.GetWorldPosition(uid) - _transform.GetWorldPosition(target)).Length();
            if (distance > comp.DriftBreakRange + comp.DriftBreakTolerance &&
                now - comp.StartTime >= comp.RefundGracePeriod)
            {
                EndLatch(uid, comp);
                continue;
            }

            // Pulled round a corner by the joint; a pinned target can't swing back through
            // the wall, so break the latch if it doesn't clear up quickly. Refund it
            // if the obstruction began right as the latch landed (an unlucky snap).
            // Slowed targets can still walk, so they're expected to fix this themselves:
            // the latch sticks and keeps biting.
            if (IsPinnedTarget(target) && !HasLatchLineOfSight(uid, target))
            {
                comp.ObstructedSince ??= now;
                if (now - comp.ObstructedSince.Value >= comp.ObstructionBreakDelay)
                {
                    EndLatch(uid, comp, comp.ObstructedSince.Value - comp.StartTime < comp.RefundGracePeriod);
                    continue;
                }
            }
            else
            {
                comp.ObstructedSince = null;
            }

            // Re-assert every tick so this can't be toggled back on mid-latch.
            _combatMode.SetInCombatMode(uid, false);

            if (TryComp<LatchStruggleComponent>(target, out var struggle))
                UpdateStruggle(target, struggle, comp, now);

            if (!comp.TickPaused && comp.ObstructedSince == null && now >= comp.NextTickTime)
            {
                DealTick(uid, comp, target);
                comp.NextTickTime = now + comp.TickInterval;
            }
        }
    }
}
