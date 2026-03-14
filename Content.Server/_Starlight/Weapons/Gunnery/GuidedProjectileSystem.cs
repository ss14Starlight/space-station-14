using System.Numerics;
using Content.Shared._Starlight.Weapons.Gunnery;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Starlight.Weapons.Gunnery;

/// <summary>
/// Steers guided projectiles (<see cref="GuidedProjectileComponent"/>) toward their
/// <see cref="GuidedProjectileComponent.SteeringTarget"/> every physics frame, limited
/// by the projectile's <see cref="GuidedProjectileComponent.TurnRate"/>.
/// </summary>
public sealed class GuidedProjectileSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem    _physics   = default!;
    [Dependency] private readonly SharedTransformSystem  _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = AllEntityQuery<GuidedProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var guided, out var physics, out var xform))
        {
            if (!guided.Active)
                continue;

            var currentSpeed = physics.LinearVelocity.Length();
            if (currentSpeed < 0.1f)
                continue;

            var currentPos = _transform.GetMapCoordinates(uid, xform).Position;
            var toTarget   = guided.SteeringTarget - currentPos;

            if (toTarget.LengthSquared() < 0.01f)
                continue;

            var desiredDir  = Vector2.Normalize(toTarget);
            var currentDir  = Vector2.Normalize(physics.LinearVelocity);

            // Maximum rotation this frame (radians).
            var maxTurn = float.DegreesToRadians(guided.TurnRate) * frameTime;

            var dot = Math.Clamp(Vector2.Dot(currentDir, desiredDir), -1f, 1f);
            var angleToTarget = MathF.Acos(dot);

            Vector2 newDir;
            if (angleToTarget <= maxTurn)
            {
                // Can fully align this frame.
                newDir = desiredDir;
            }
            else
            {
                // Rotate currentDir toward desiredDir by maxTurn.
                // Cross product sign determines rotation direction.
                var cross      = currentDir.X * desiredDir.Y - currentDir.Y * desiredDir.X;
                var rotAngle   = cross >= 0f ? maxTurn : -maxTurn;
                var cos        = MathF.Cos(rotAngle);
                var sin        = MathF.Sin(rotAngle);
                newDir = new Vector2(
                    cos * currentDir.X - sin * currentDir.Y,
                    sin * currentDir.X + cos * currentDir.Y);
            }

            _physics.SetLinearVelocity(uid, newDir * currentSpeed, body: physics);
        }
    }
}
