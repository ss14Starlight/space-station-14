using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Gravity;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Pulling.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Actions.EntitySystems;

public abstract partial class SharedLatchSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatchComponent, RefreshMovementSpeedModifiersEvent>(OnLatcherRefreshMovementSpeed);
        SubscribeLocalEvent<LatchedComponent, RefreshMovementSpeedModifiersEvent>(OnTargetRefreshMovementSpeed);
        SubscribeLocalEvent<LatchComponent, RefreshWeightlessModifiersEvent>(OnLatcherRefreshWeightless);
        SubscribeLocalEvent<LatchedComponent, RefreshWeightlessModifiersEvent>(OnTargetRefreshWeightless);
        SubscribeLocalEvent<LatchComponent, IsWeightlessEvent>(OnLatcherIsWeightless);

        SubscribeLocalEvent<LatchComponent, AttackAttemptEvent>(OnLatcherAttackAttempt);

        SubscribeLocalEvent<LatchComponent, BeingPulledAttemptEvent>(OnLatcherBeingPulledAttempt);
        SubscribeLocalEvent<LatchedComponent, BeingPulledAttemptEvent>(OnTargetBeingPulledAttempt);

        SubscribeLocalEvent<LatchActionEvent>(OnLatchAction);
    }

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

        CreateLatchJoint(uid, comp, target);
        StartLatch(uid, comp, target);
        ev.Handled = true;
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

    /// <summary>
    /// Latcher is weightless while latched to a floating target.
    /// </summary>
    private void OnLatcherIsWeightless(EntityUid uid, LatchComponent comp, ref IsWeightlessEvent ev)
    {
        if (!comp.Active || !comp.LatcherWeightless)
            return;

        ev.IsWeightless = true;
        ev.Handled = true;
    }

    private void OnTargetRefreshMovementSpeed(EntityUid uid, LatchedComponent comp, RefreshMovementSpeedModifiersEvent ev)
        => ev.ModifySpeed(comp.SpeedMultiplier);

    // Weightless movement reads WeightlessModifier, not the walk/sprint modifiers,
    // so the latch has to apply its speed changes here too.
    private void OnLatcherRefreshWeightless(EntityUid uid, LatchComponent comp, ref RefreshWeightlessModifiersEvent ev)
    {
        if (comp.Active)
            ev.ModifyAcceleration(1f, 0f);
    }

    private void OnTargetRefreshWeightless(EntityUid uid, LatchedComponent comp, ref RefreshWeightlessModifiersEvent ev)
        => ev.ModifyAcceleration(1f, comp.SpeedMultiplier);

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
    /// Struggle cursor position at the given time, unfolded over 0 to 2
    /// (0 to 1 travelling right, 1 to 2 travelling back left).
    /// </summary>
    public static float GetStruggleUnfolded(LatchStruggleComponent struggle, TimeSpan time)
    {
        if (time <= struggle.SegmentStart)
            return struggle.SegmentPosition;

        var travelled = struggle.Speed * (float) (time - struggle.SegmentStart).TotalSeconds;
        var unfolded = (struggle.SegmentPosition + travelled) % 2f;
        return unfolded < 0f ? unfolded + 2f : unfolded;
    }

    /// <summary>
    /// Struggle cursor position on the bar at the given time, 0 to 1.
    /// </summary>
    public static float GetStruggleCursor(LatchStruggleComponent struggle, TimeSpan time)
    {
        var unfolded = GetStruggleUnfolded(struggle, time);
        return unfolded <= 1f ? unfolded : 2f - unfolded;
    }

    /// <summary>
    /// Grades a cursor position against a zone: perfect in the middle, good on either side.
    /// </summary>
    public static LatchStruggleResult GradeStruggle(float cursor, float zoneCenter, float perfectWidth, float goodWidth)
    {
        var distance = MathF.Abs(cursor - zoneCenter);
        var halfPerfect = perfectWidth / 2f;

        if (distance <= halfPerfect)
            return LatchStruggleResult.Perfect;

        return distance <= halfPerfect + goodWidth
            ? LatchStruggleResult.Good
            : LatchStruggleResult.Miss;
    }
}
