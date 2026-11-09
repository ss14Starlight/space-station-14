using System.Linq;
using Content.Server.Actions;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Charges.Systems;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Actions.EntitySystems;

public sealed partial class FelionoidPounceSystem : EntitySystem
{
    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrownItemSystem _thrown = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan _pounceChargeDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _pounceTimeout = TimeSpan.FromSeconds(2);

    [SubscribeLocalEvent]
    private void OnStartup(EntityUid uid, FelionoidPounceComponent comp, ComponentStartup ev)
        => _action.AddAction(uid, ref comp.ActionEntity, comp.Action);
    [SubscribeLocalEvent]
    private void OnShutdown(EntityUid uid, FelionoidPounceComponent comp, ComponentShutdown ev)
    {
        _action.RemoveAction(uid, comp.ActionEntity);
        comp.ChargingTarget = null;
        comp.PouncingTarget = null;
    }
    [SubscribeLocalEvent]
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
    [SubscribeLocalEvent]
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

        if (args.Cancelled)
        {
            if (comp.ActionEntity is { } actionEntity)
            {
                _charges.AddCharges((actionEntity, null, null), 1);
                _action.ClearCooldown(actionEntity);
            }

            return;
        }

        if (target is not { } pounceTarget
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
    [SubscribeLocalEvent]
    private void OnPounceCollide(EntityUid uid, FelionoidPounceComponent comp, ref StartCollideEvent args)
    {
        if (comp.PouncingTarget is not { } target
            || args.OtherEntity != target
            || !Exists(target))
            return;

        comp.PouncingTarget = null;
        comp.PounceEndTime = TimeSpan.Zero;

        FinishPounce(uid, comp, target);

        if (TryComp<ThrownItemComponent>(uid, out var thrown))
            _thrown.StopThrow(uid, thrown);
    }

    private void FinishPounce(EntityUid uid, FelionoidPounceComponent comp, EntityUid target)
    {
        _chat.TryEmoteWithoutChat(target, "Scream");

        if (TryPickupPouncer(target, uid))
            return;

        var duration = TimeSpan.FromSeconds(comp.KnockdownDuration);
        _stun.TryKnockdown(uid, duration, force: true);
        _stun.TryKnockdown(target, duration, force: true);
    }

    private bool TryPickupPouncer(EntityUid target, EntityUid pouncer)
    {
        if (!TryComp<HandsComponent>(target, out _)
            || !HasComp<ItemComponent>(pouncer))
            return false;

        var heldItems = _hands.EnumerateHeld(target).ToArray();
        if (heldItems.Length > 0) return false;

        return _hands.TryPickupAnyHand(target, pouncer, animate: false);
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
