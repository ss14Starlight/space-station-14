using System.Numerics;

namespace Content.Client._Starfall.Particles;

/// <summary>
/// A single live particle. Class so it can be pooled in place.
/// </summary>
public sealed class ParticleData
{
    /// <summary>
    /// Screen-space offset from the emitter origin, X = right, Y = up.
    /// All simulation runs in this space regardless of <see cref="Content.Shared._Starfall.Particles.ParticleEffectPrototype.WorldSpace"/>.
    /// </summary>
    public Vector2 LocalOffset;

    /// <summary>
    /// World position at spawn time, used for world-space particles.
    /// Draw position = SpawnOrigin + rotate(LocalOffset, -eyeRot).
    /// Unused for screen-space particles.
    /// </summary>
    public Vector2 SpawnOrigin;

    public Vector2 Velocity;        // current movement vector in screen-space units/sec
    public TimeSpan Age;            // how long this particle has been alive
    public TimeSpan Lifetime;       // total lifespan before the particle dies
    public float SpawnSpeed;        // speed magnitude at spawn, used by SpeedOverLifetime
    public float SpawnIntensity;    // emitter intensity captured at spawn, used to scale rendered size
    public float Rotation;          // current rotation in radians
    public float RotationSpeed;     // spin rate in radians per second
    public bool Alive;              // false = dead and available for pooling

    /// <summary>Size multiplier baked in at spawn from SizeVariance.</summary>
    public float SizeMultiplier = 1f;

    /// <summary>Noise seed so each particle gets different turbulence.</summary>
    public Vector2 NoiseOffset;

    public float AgeRatio => Lifetime > TimeSpan.Zero ? Math.Clamp((float)(Age.TotalSeconds / Lifetime.TotalSeconds), 0f, 1f) : 1f;

    public void Reset()
    {
        LocalOffset = default;
        SpawnOrigin = default;
        Velocity = default;
        Age = TimeSpan.Zero;
        Lifetime = TimeSpan.FromSeconds(1);
        SpawnSpeed = 0f;
        SpawnIntensity = 1f;
        Rotation = 0f;
        RotationSpeed = 0f;
        Alive = false;
        SizeMultiplier = 1f;
        NoiseOffset = default;
    }
}
