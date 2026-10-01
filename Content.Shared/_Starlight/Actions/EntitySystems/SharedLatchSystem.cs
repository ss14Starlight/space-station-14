using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Pulling.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Actions.EntitySystems;

public abstract partial class SharedLatchSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatchComponent, RefreshMovementSpeedModifiersEvent>(OnLatcherRefreshMovementSpeed);
        SubscribeLocalEvent<LatchedComponent, RefreshMovementSpeedModifiersEvent>(OnTargetRefreshMovementSpeed);

        SubscribeLocalEvent<LatchComponent, AttackAttemptEvent>(OnLatcherAttackAttempt);
        SubscribeLocalEvent<LatchBlockedHandComponent, ContainerGettingRemovedAttemptEvent>(OnBlockedHandRemoveAttempt);

        SubscribeLocalEvent<LatchComponent, BeingPulledAttemptEvent>(OnLatcherBeingPulledAttempt);
        SubscribeLocalEvent<LatchedComponent, BeingPulledAttemptEvent>(OnTargetBeingPulledAttempt);

        SubscribeLocalEvent<LatchActionEvent>(OnLatchAction);
    }

    /// <summary>
    /// Validates a latch attempt (whitelist, existing latches, and anything
    /// cancelling <see cref="LatchAttemptEvent"/>), then starts the latch.
    /// </summary>
    private void OnLatchAction(LatchActionEvent ev)
    {
        if (ev.Handled)
            return;

        var uid = ev.Performer;
        if (!TryComp<LatchComponent>(uid, out var comp) || comp.Active)
            return;

        var target = ev.Target;
        if (target == uid || HasComp<LatchedComponent>(target))
            return;

        if (!_whitelist.IsWhitelistPassOrNull(comp.Whitelist, target))
            return;

        var attempt = new LatchAttemptEvent(uid, target);
        RaiseLocalEvent(target, ref attempt);
        if (attempt.Cancelled)
            return;

        CreateLatchJoint(uid, comp, target);
        StartLatch(uid, comp, target);
        ev.Handled = true;
    }

    /// <summary>
    /// Frees <paramref name="target"/> from whatever is latched onto it. For
    /// target-side escape abilities (e.g. Rejuvenate, Biodegrade).
    /// </summary>
    /// <returns>True if an active latch was found and ended.</returns>
    public bool TryBreakLatch(Entity<LatchedComponent?> target)
    {
        if (!Resolve(target, ref target.Comp, false)
            || !TryComp<LatchComponent>(target.Comp.Latcher, out var latcherComp)
            || !latcherComp.Active
            || latcherComp.Target != target.Owner)
        {
            return false;
        }

        BreakLatch((target.Comp.Latcher, latcherComp));
        return true;
    }

    /// <summary>
    /// Forces <paramref name="latcher"/> to let go of its current target. For
    /// abilities that act on the latcher rather than the target.
    /// </summary>
    /// <returns>True if an active latch was found and ended.</returns>
    public bool TryReleaseLatch(Entity<LatchComponent?> latcher)
    {
        if (!Resolve(latcher, ref latcher.Comp, false) || !latcher.Comp.Active)
            return false;

        BreakLatch((latcher.Owner, latcher.Comp));
        return true;
    }

    /// <summary>
    /// Authoritative latch teardown. Overridden serverside.
    /// </summary>
    protected virtual void BreakLatch(Entity<LatchComponent> latcher)
    {
    }

    /// <summary>
    /// Center-to-center raycast with the same mask melee uses, so "obstructed"
    /// here means exactly what stops the target from hitting back.
    /// </summary>
    protected bool HasLatchLineOfSight(EntityUid latcher, EntityUid target)
    {
        var from = _transform.GetMapCoordinates(latcher);
        var to = _transform.GetMapCoordinates(target);
        return _interaction.InRangeUnobstructed(from, to, range: 0f, predicate: e => e == latcher || e == target);
    }

    /// <summary>
    /// Authoritative side-effects of starting a latch. Overridden serverside.
    /// </summary>
    protected virtual void StartLatch(EntityUid uid, LatchComponent comp, EntityUid target)
    {
    }

    /// <summary>
    /// Creates the physics joint between latcher and target.
    /// </summary>
    protected void CreateLatchJoint(EntityUid uid, LatchComponent comp, EntityUid target)
    {
        if (Timing.ApplyingState)
            return;

        comp.LatchJointId = $"latch-joint-{GetNetEntity(uid)}";
        var joint = _joints.CreateDistanceJoint(uid, target, id: comp.LatchJointId);
        joint.CollideConnected = false;
        joint.MinLength = 0f;
        joint.MaxLength = comp.MaxJointLength;
        joint.Stiffness = 0f;
    }

    private void OnLatcherRefreshMovementSpeed(EntityUid uid, LatchComponent comp, RefreshMovementSpeedModifiersEvent ev)
    {
        if (comp.Active)
            ev.ModifySpeed(0f);
    }

    private void OnTargetRefreshMovementSpeed(EntityUid uid, LatchedComponent comp, RefreshMovementSpeedModifiersEvent ev)
    {
        ev.ModifySpeed(0f);
    }

    /// <summary>
    /// Blocks manual attacks while latched, so Bite Harder is the only option.
    /// </summary>
    private void OnLatcherAttackAttempt(EntityUid uid, LatchComponent comp, AttackAttemptEvent ev)
    {
        if (comp.Active)
            ev.Cancel();
    }

    private void OnLatcherBeingPulledAttempt(Entity<LatchComponent> ent, ref BeingPulledAttemptEvent args)
    {
        if (ent.Comp.Active)
            args.Cancel();
    }

    private void OnTargetBeingPulledAttempt(Entity<LatchedComponent> ent, ref BeingPulledAttemptEvent args)
    {
        if (TryComp<LatchComponent>(ent.Comp.Latcher, out var latchComp) && latchComp.Active)
            args.Cancel();
    }

    /// <summary>
    /// The latch's blocked hand can't be dropped via the drop key.
    /// </summary>
    private void OnBlockedHandRemoveAttempt(EntityUid uid, LatchBlockedHandComponent comp, ref ContainerGettingRemovedAttemptEvent args)
    {
        args.Cancel();
    }
}
