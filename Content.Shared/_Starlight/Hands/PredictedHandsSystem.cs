using System.Numerics;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared._Starlight.Combat.Disarming;
using Content.Shared.ActionBlocker;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Hands;

public sealed partial class PredictedHandsSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    /// <summary>
    /// Items dropped when the holder falls down will be launched in
    /// a direction offset by up to this many degrees from the holder's
    /// movement direction.
    /// </summary>
    private const float DropHeldItemsSpread = 45;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, DisarmedEvent>(OnDisarmed, before: new[] {typeof(SharedStunSystem), typeof(SharedStaminaSystem)});
        SubscribeLocalEvent<HandsComponent, DropHandItemsEvent>(OnDropHandItems);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ThrowItemInHand, new PointerInputCmdHandler(HandleThrowItem))
            .Register<PredictedHandsSystem>();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
    }

    public override void Shutdown() => CommandBinds.Unregister<PredictedHandsSystem>();

    private void OnDisarmed(EntityUid uid, HandsComponent component, ref DisarmedEvent args)
    {
        if (args.Handled)
            return;

        // Break any pulls
        if (TryComp(uid, out PullerComponent? puller) && TryComp(puller.Pulling, out PullableComponent? pullable))
            _pulling.TryStopPull(puller.Pulling.Value, pullable);

        if (HasComp<NoDisarmComponent>(_hands.GetActiveItem(args.Target))) return;
        var offset = _random.NextAnglePredicted(_timing)
            .RotateVec(new Vector2(_random.NextFloatPredicted(_timing, 1, 1.5f), 0));
        var offsetRandomCoordinates = _xform.GetMoverCoordinates(args.Target).Offset(offset);
        if (!ThrowHeldItem(args.Target, offsetRandomCoordinates))
            return;

        args.PopupPrefix = "disarm-action-";

        args.Handled = true; // no shove/stun.
    }

    private bool HandleThrowItem(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
    {
        if (playerSession?.AttachedEntity is not {Valid: true} player || !Exists(player) || !coordinates.IsValid(EntityManager))
            return false;

        ThrowHeldItem(player, coordinates);
        return false;
    }

    /// <summary>
    /// Throw the player's currently held item.
    /// </summary>
    public bool ThrowHeldItem(EntityUid player, EntityCoordinates coordinates, float minDistance = 0.1f)
    {
        if (_container.IsEntityInContainer(player) ||
            !TryComp(player, out HandsComponent? hands) ||
            !_hands.TryGetActiveItem((player, hands), out var throwEnt) ||
            !_actionBlocker.CanThrow(player, throwEnt.Value))
            return false;

        if (_timing.CurTime < hands.NextThrowTime)
            return false;
        hands.NextThrowTime = _timing.CurTime + hands.ThrowCooldown;
        Dirty(player, hands);

        if (TryComp(throwEnt, out StackComponent? stack) && stack.Count > 1 && stack.ThrowIndividually)
        {
            var splitStack = _stack.Split((throwEnt.Value, stack), 1, Comp<TransformComponent>(player).Coordinates);

            if (splitStack is not {Valid: true})
                return false;

            throwEnt = splitStack.Value;
        }

        var direction = _xform.ToMapCoordinates(coordinates).Position - _xform.GetWorldPosition(player);
        if (direction == Vector2.Zero)
            return true;

        var length = direction.Length();
        var distance = Math.Clamp(length, minDistance, hands.ThrowRange);
        direction *= distance / length;

        var throwSpeed = hands.BaseThrowspeed;

        // Let other systems change the thrown entity (useful for virtual items)
        // or the throw strength.
        var ev = new BeforeThrowEvent(throwEnt.Value, direction, throwSpeed, player);
        RaiseLocalEvent(player, ref ev);

        if (ev.Cancelled)
            return true;

        // This can grief the above event so we raise it afterwards
        if (_hands.IsHolding((player, hands), throwEnt, out _) && !_hands.TryDrop(player, throwEnt.Value))
            return false;

        _throwing.TryThrow(ev.ItemUid, ev.Direction, ev.ThrowSpeed, ev.PlayerUid, compensateFriction: !HasComp<LandAtCursorComponent>(ev.ItemUid));

        return true;
    }

    private void OnDropHandItems(Entity<HandsComponent> entity, ref DropHandItemsEvent args)
    {
        // If the holder doesn't have a physics component, they ain't moving
        var holderVelocity = _physicsQuery.TryComp(entity, out var physics) ? physics.LinearVelocity : Vector2.Zero;
        var spreadMaxAngle = Angle.FromDegrees(DropHeldItemsSpread);

        foreach (var hand in entity.Comp.Hands.Keys)
        {
            if (!_hands.TryGetHeldItem(entity.AsNullable(), hand, out var heldEntity))
                continue;

            var throwAttempt = new FellDownThrowAttemptEvent(entity);
            RaiseLocalEvent(heldEntity.Value, ref throwAttempt);

            if (throwAttempt.Cancelled)
                continue;

            if (!_hands.TryDrop(entity.AsNullable(), hand, checkActionBlocker: false))
                continue;

            // Rotate the item's throw vector a bit for each item
            var angleOffset = _random.NextAnglePredicted(_timing, -spreadMaxAngle, spreadMaxAngle);
            // Rotate the holder's velocity vector by the angle offset to get the item's velocity vector
            var itemVelocity = angleOffset.RotateVec(holderVelocity);
            // Decrease the distance of the throw by a random amount
            itemVelocity *= _random.NextFloatPredicted(_timing, 1f);
            // Heavier objects don't get thrown as far
            // If the item doesn't have a physics component, it isn't going to get thrown anyway, but we'll assume infinite mass
            itemVelocity *= _physicsQuery.TryComp(heldEntity, out var heldPhysics) ? heldPhysics.InvMass : 0;
            // Throw at half the holder's intentional throw speed and
            // vary the speed a little to make it look more interesting
            var throwSpeed = entity.Comp.BaseThrowspeed * _random.NextFloatPredicted(_timing, 0.45f, 0.55f);

            _throwing.TryThrow(heldEntity.Value,
                itemVelocity,
                throwSpeed,
                entity,
                pushbackRatio: 0,
                compensateFriction: false
            );
        }
    }
}
