using System.Numerics;
using System.Runtime.CompilerServices;

namespace Content.Shared._Starlight.Movement;

public static class MoverSolver
{
    /// <summary>
    /// everything the solve needs, after the controller already decoded the wish dir and looked up the
    /// tile-multiplied friction/accel. nothing else.
    /// </summary>
    public struct MoverParams
    {
        /// <summary>current linear velocity (body's LinearVelocity).</summary>
        public Vector2 Velocity;

        /// <summary>current spin. any spin kills the idle fast-path.</summary>
        public float AngularVelocity;

        /// <summary>wish dir scaled by walk/sprint speed (post AssertValidWish). zero = no input.</summary>
        public Vector2 WishDir;

        /// <summary>friction when there IS input, already tile-multiplied.</summary>
        public float FrictionInput;

        /// <summary>friction when there is NO input, already tile-multiplied.</summary>
        public float FrictionNoInput;

        /// <summary>acceleration, already tile-multiplied.</summary>
        public float Accel;

        /// <summary>speed floor below which friction doesn't apply.</summary>
        public float MinimumFrictionSpeed;

        /// <summary>engine-wide min damping floor (_minDamping).</summary>
        public float MinDamping;

        /// <summary>body weightless this tick?</summary>
        public bool Weightless;

        /// <summary>weightless body touching something to push off?</summary>
        public bool Touching;
    }

    /// <summary>
    /// solves one mover. true + new velocity when it did work, false when the idle fast-path fired and
    /// velocity is untouched (controller leaves the body alone, same as before).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySolve(in MoverParams p, float frameTime, out Vector2 newVelocity)
    {
        newVelocity = p.Velocity;
        var hasWish = p.WishDir != Vector2.Zero;

        // no input, no spin, already settled under the friction floor -> provable no-op
        // (Friction early-returns, Accelerate early-returns on a zero wish dir). bail.
        if (!hasWish
            && p.AngularVelocity == 0f
            && p.Velocity.LengthSquared() < p.MinimumFrictionSpeed * p.MinimumFrictionSpeed)
        {
            return false;
        }

        var friction = hasWish ? p.FrictionInput : p.FrictionNoInput;

        // friction never beats accel while moving.
        if (hasWish)
            friction = MathF.Min(friction, p.Accel);
        friction = MathF.Max(friction, p.MinDamping);

        Friction(p.MinimumFrictionSpeed, frameTime, friction, ref newVelocity);

        if (!p.Weightless || p.Touching)
            Accelerate(ref newVelocity, in p.WishDir, p.Accel, frameTime);

        return true;
    }

    public static void Friction(float minimumFrictionSpeed, float frameTime, float friction, ref Vector2 velocity)
    {
        var speed = velocity.Length();
        if (speed < minimumFrictionSpeed)
            return;

        velocity *= Math.Clamp(1.0f - frameTime * friction, 0.0f, 1.0f);
    }

    public static void Accelerate(ref Vector2 currentVelocity, in Vector2 velocity, float accel, float frameTime)
    {
        var wishDir = velocity != Vector2.Zero ? Vector2.Normalize(velocity) : Vector2.Zero;
        var wishSpeed = velocity.Length();

        var currentSpeed = Vector2.Dot(currentVelocity, wishDir);
        var addSpeed = wishSpeed - currentSpeed;

        if (addSpeed <= 0f)
            return;

        var accelSpeed = accel * frameTime * wishSpeed;
        accelSpeed = MathF.Min(accelSpeed, addSpeed);

        currentVelocity += wishDir * accelSpeed;
    }
}
