using Content.Server.Actions;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Actions.EntitySystems;

public sealed partial class FelionoidPounceSystem : EntitySystem
{
    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan _pounceChargeDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _pounceTimeout = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FelionoidPounceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FelionoidPounceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FelionoidPounceComponent, ThrowDoHitEvent>(OnPounceHit);
        SubscribeLocalEvent<FelionoidPounceComponent, FelionoidPounceDoAfterEvent>(OnPounceDoAfter);
        SubscribeLocalEvent<FelionoidPounceActionEvent>(OnPounceAction);
    }

    private void OnStartup(EntityUid uid, FelionoidPounceComponent comp, ComponentStartup ev)
        => _action.AddAction(uid, ref comp.ActionEntity, comp.Action);

    private void OnShutdown(EntityUid uid, FelionoidPounceComponent comp, ComponentShutdown ev)
    {
        _action.RemoveAction(uid, comp.ActionEntity);
        comp.ChargingTarget = null;
        comp.PouncingTarget = null;
    }

    private void OnPounceAction(FelionoidPounceActionEvent ev)
    {
        if (ev.Handled)
            return;

        var uid = ev.Performer;
        if (!TryComp<FelionoidPounceComponent>(uid, out var comp)
            || comp.ChargingTarget != null
            || comp.PouncingTarget != null)
            return;

        var target = ev.Target;
        if (target == uid || !Exists(target) || !HasComp<MobStateComponent>(target))
            return;

        comp.ChargingTarget = target;
        var doAfter = new DoAfterArgs(
            EntityManager,
            uid,
            _pounceChargeDuration,
            new FelionoidPounceDoAfterEvent(),
            uid,
            target)
        {
            BreakOnMove = true,
            DistanceThreshold = 3f
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            comp.ChargingTarget = null;
            return;
        }

        ev.Handled = true;
    }

    private void OnPounceDoAfter(
        Entity<FelionoidPounceComponent> ent,
        ref FelionoidPounceDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var comp = ent.Comp;
        var target = comp.ChargingTarget;
        comp.ChargingTarget = null;

        if (args.Cancelled
            || target is not { } pounceTarget
            || args.Target != pounceTarget
            || pounceTarget == ent.Owner
            || !Exists(pounceTarget)
            || !HasComp<MobStateComponent>(pounceTarget))
            return;

        var performerCoords = _transform.GetMapCoordinates(ent);
        var targetCoords = _transform.GetMapCoordinates(pounceTarget);
        if (performerCoords.MapId != targetCoords.MapId)
            return;

        var direction = targetCoords.Position - performerCoords.Position;
        if (direction.LengthSquared() <= 0.0001f)
        {
            FinishPounce(ent, comp, pounceTarget);
            return;
        }

        comp.PouncingTarget = pounceTarget;
        comp.PounceEndTime = _timing.CurTime + _pounceTimeout;
        _throwing.TryThrow(
            ent,
            direction,
            baseThrowSpeed: comp.JumpSpeed,
            compensateFriction: true,
            doSpin: false);
    }

    private void OnPounceHit(EntityUid uid, FelionoidPounceComponent comp, ref ThrowDoHitEvent args)
    {
        if (comp.PouncingTarget is not { } target)
            return;

        comp.PouncingTarget = null;
        comp.PounceEndTime = TimeSpan.Zero;

        if (args.Target != target || !Exists(target))
            return;

        FinishPounce(uid, comp, target);
    }

    private void FinishPounce(EntityUid uid, FelionoidPounceComponent comp, EntityUid target)
    {
        _chat.TryEmoteWithoutChat(target, "Scream");

        if (TryHoldPouncer(target, uid))
            return;

        var duration = TimeSpan.FromSeconds(comp.KnockdownDuration);
        _stun.TryKnockdown(uid, duration, force: true);
        _stun.TryKnockdown(target, duration, force: true);
    }

    private bool TryHoldPouncer(EntityUid target, EntityUid pouncer)
    {
        if (!HasComp<PullerComponent>(target)
            || !HasComp<PullableComponent>(pouncer)
            || !TryComp<HandsComponent>(target, out _))
            return false;

        EntityUid? itemToDrop = null;
        foreach (var heldItem in _hands.EnumerateHeld(target))
        {
            if (!_hands.CanDrop(target, heldItem))
                return false;

            itemToDrop ??= heldItem;
        }

        if (itemToDrop is not { } item || !_hands.TryDrop(target, item))
            return false;

        return _pulling.TryStartPull(target, pouncer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FelionoidPounceComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.PouncingTarget == null || now < comp.PounceEndTime)
                continue;

            comp.PouncingTarget = null;
            comp.PounceEndTime = TimeSpan.Zero;
        }
    }
}
