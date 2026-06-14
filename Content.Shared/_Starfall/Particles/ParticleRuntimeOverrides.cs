using System.Numerics;

namespace Content.Shared._Starfall.Particles;

/// <summary>
/// Per-emitter runtime overrides for <see cref="ParticleEffectPrototype"/> fields.
/// Every field is nullable — null means "use the prototype value". Only set what you need to change.
/// </summary>
public sealed class ParticleRuntimeOverrides
{
    #region =^..^= Visuals =^..^=

    public Color? StartColor; // self-explanatory lerps to EndColor over the particle's lifetime linearly.
    public Color? EndColor; // self-explanatory

    /// <summary>Global tint multiplied on top of every particle's color.</summary>
    public Color? ColorOverride;

    public string? Shader; // The shader to use for this emitter's particles. Falls back to the prototype's shader if null.
    public int? RenderLayer; // The render layer to draw these particles on. Falls back to the prototype's layer if null.
    #endregion
    #region =^..^= Size =^..^=

    public float? ParticleSize; // The base size of each particle.
    public float? SizeVariance; // Random variance added to each particle's size at spawn, in the range [-SizeVariance, SizeVariance].
    public float? StretchFactor; // Multiplies the particle's length along its velocity vector, creating a stretched streak effect. 1.0 = normal.

    #endregion
    #region  =^..^= Lifetime =^..^=

    public TimeSpan? Lifetime; // How long each particle lives before disappearing.
    public TimeSpan? LifetimeVariance; // Random variance added to each particle's lifetime at spawn, in the range [-LifetimeVariance, LifetimeVariance].

    #endregion
    #region =^..^= Movement =^..^=

    public float? Speed; // Initial speed of each particle at spawn.
    public float? SpeedVariance; // Random variance added to each particle's initial speed at spawn, in the range [-SpeedVariance, SpeedVariance].
    public Vector2? ConstantForce; // A constant acceleration applied to each particle every tick, X = right, Y = up.
    public float? Gravity; // Additional downward (or upward with negative values) acceleration applied to each particle every tick. Added on top of ConstantForce.Y.
    public float? Drag; // Multiplier applied to each particle's velocity every tick, simulating air resistance. 1.0 = no drag, 0.5 = velocity halved every tick.
    public float? TerminalSpeed; // Maximum speed for each particle. If non-null, velocity is clamped to this magnitude every tick after applying forces and drag.
    public float? NoiseStrength; // Strength of the procedural noise turbulence applied to each particle every tick, in screen-space units. Noise is a curl field based on Perlin noise.
    public float? NoiseFrequency; // Frequency of the procedural noise turbulence. Higher values create smaller, more chaotic swirls, while lower values create larger, smoother waves.
    public float? InheritVelocity; // Multiplier for how much of the emitter's current velocity is inherited by each particle at spawn. 0.0 = no inheritance, 1.0 = particles spawn with the same velocity as the emitter.

    #endregion
    #region =^..^= Rotation =^..^=

    public Angle? StartRotation; // Initial rotation of each particle at spawn, in radians. 0 = facing right, positive = clockwise.
    public Angle? StartRotationVariance; // Random variance added to each particle's initial rotation at spawn, in radians, in the range [-StartRotationVariance, StartRotationVariance].
    public Angle? RotationSpeed; // Spin rate of each particle in radians per second. Positive values spin clockwise, negative values spin counterclockwise.
    public Angle? RotationSpeedVariance; // Random variance added to each particle's rotation speed at spawn, in radians per second, in the range [-RotationSpeedVariance, RotationSpeedVariance].

    #endregion
    #region =^..^= Emission =^..^=

    public float? EmissionRate; // Number of particles emitted per second while the emitter is active.
    public int? MaxCount; // Max live particles at once. Be careful raising this at runtime, the pool only grows, never shrinks.
                          // Increasing MaxCount after spawn causes new allocations beyond the original pool size.
                          // Decreasing it is safe but the extra slots stay allocated until the emitter is destroyed.
    public TimeSpan? Duration; // How long the emitter produces new particles before stopping. Existing particles live out their lifetimes. Null means infinite duration.
    public Angle? SpreadAngle; // Random variance added to each particle's initial movement angle at spawn, in radians, in the range [-SpreadAngle/2, SpreadAngle/2]. 0 means all particles move in the same direction.
    public Angle? EmitAngle; // Base movement angle of each particle at spawn, in radians. 0 = facing right, positive = clockwise. SpreadAngle is added on top of this base angle as a random variance.

    #endregion
    #region =^..^= Spawn position =^..^=

    /// <summary>World-space offset from the emitter origin where particles are spawned.</summary>
    public Vector2? SpawnOffset;

    #endregion
}
