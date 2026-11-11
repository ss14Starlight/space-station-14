using System.Numerics;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Throwing;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Throwing;

public sealed partial class ReturnOnThrowSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReturnOnThrowComponent, StopThrowEvent>(OnStopThrow);
        SubscribeLocalEvent<ReturnOnThrowComponent, GotEquippedHandEvent>(OnGotEquipped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ReturnOnThrowComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var physics, out var xform))
        {
            if (component.ReturnTarget is not { } target)
                continue;

            if (!Exists(target) || !TryComp(target, out TransformComponent? targetXform))
            {
                StopReturning(uid, component, physics);
                continue;
            }

            var itemCoords = _transform.GetMapCoordinates(uid, xform);
            var targetCoords = _transform.GetMapCoordinates(target, targetXform);
            if (itemCoords.MapId != targetCoords.MapId)
            {
                StopReturning(uid, component, physics);
                continue;
            }

            var difference = targetCoords.Position - itemCoords.Position;
            if (difference.LengthSquared() <= 0.25f)
            {
                StopReturning(uid, component, physics);
                ReturnToThrower(uid, target);
                continue;
            }

            _physics.SetLinearVelocity(uid, difference.Normalized() * component.ReturnSpeed, body: physics);
        }
    }

    private void OnStopThrow(Entity<ReturnOnThrowComponent> ent, ref StopThrowEvent args)
    {
        if (args.User is not { } thrower || !Exists(thrower))
            return;

        var item = ent.Owner;
        Timer.Spawn(0, () =>
        {
            if (!Exists(thrower)
                || !Exists(item)
                || !TryComp(item, out ReturnOnThrowComponent? component)
                || !TryComp(item, out PhysicsComponent? physics)
                || !TryComp(thrower, out TransformComponent? throwerXform)
                || !TryComp(item, out TransformComponent? itemXform))
                return;

            var itemCoords = _transform.GetMapCoordinates(item, itemXform);
            var throwerCoords = _transform.GetMapCoordinates(thrower, throwerXform);
            if (itemCoords.MapId != throwerCoords.MapId)
                return;

            var difference = throwerCoords.Position - itemCoords.Position;
            if (difference.LengthSquared() <= 0.25f)
            {
                ReturnToThrower(item, thrower);
                return;
            }

            component.ReturnTarget = thrower;
            _physics.SetBodyStatus(item, physics, BodyStatus.InAir);
        });
    }

    private void OnGotEquipped(Entity<ReturnOnThrowComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.ReturnTarget == null)
            return;

        if (TryComp<PhysicsComponent>(ent, out var physics))
            StopReturning(ent, ent.Comp, physics);
        else
            ent.Comp.ReturnTarget = null;
    }

    private void StopReturning(EntityUid uid, ReturnOnThrowComponent component, PhysicsComponent physics)
    {
        component.ReturnTarget = null;
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        _physics.SetBodyStatus(uid, physics, BodyStatus.OnGround);
    }

    private void ReturnToThrower(EntityUid item, EntityUid thrower)
    {
        if (!Exists(item) || !Exists(thrower))
            return;

        _hands.TryForcePickupAnyHand(thrower, item);
    }
}
