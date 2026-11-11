using Content.Shared._Starlight.Light.Components;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Starlight.Light;

[ByRefEvent]
public record struct DropRotationEvent(EntityUid User, Angle Rotation);

public sealed partial class FaceAwayWhenDroppedSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    [SubscribeLocalEvent]
    private void OnDropRotation(Entity<FaceAwayWhenDroppedComponent> ent, ref DropRotationEvent args)
    {
        var parent = Transform(ent).ParentUid;
        args.Rotation = _transform.GetWorldRotation(args.User) - _transform.GetWorldRotation(parent);
    }

    [SubscribeLocalEvent]
    private void OnLand(Entity<FaceAwayWhenDroppedComponent> ent, ref LandEvent args)
        => FaceAwayFrom(ent, args.User);

    [SubscribeLocalEvent]
    private void OnStopThrow(Entity<FaceAwayWhenDroppedComponent> ent, ref StopThrowEvent args)
        => FaceAwayFrom(ent, args.User);

    private void FaceAwayFrom(EntityUid uid, EntityUid? user)
    {
        if (user is not { } thrower || TerminatingOrDeleted(thrower))
            return;

        var xform = Transform(uid);
        var userXform = Transform(thrower);
        if (xform.MapID != userXform.MapID)
            return;

        var direction = _transform.GetWorldPosition(xform) - _transform.GetWorldPosition(userXform);
        if (direction.LengthSquared() < 0.01f)
            return;

        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.SetAngularVelocity(uid, 0f, body: physics);

        // Angle zero looks south, same as ToWorldAngle.
        _transform.SetWorldRotation(xform, direction.ToWorldAngle());
    }
}
