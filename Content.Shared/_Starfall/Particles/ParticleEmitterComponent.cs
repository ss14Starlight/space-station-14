using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starfall.Particles;

/// <summary>
/// Spawns a particle effect on this entity when it initializes.
/// </summary>
[RegisterComponent]
public sealed partial class ParticleEmitterComponent : Component
{
    /// <summary>The particle effect to emit.</summary>
    [DataField(required: true)]
    public ProtoId<ParticleEffectPrototype> Effect;

    /// <summary>
    /// Optional color tint applied to every particle of this emitter.
    /// Multiplied on top of the prototype colors; null leaves colors unchanged.
    /// </summary>
    [DataField]
    public Color? ColorOverride;

    /// <summary>
    /// Starting intensity multiplier (0–1+). Scales emission rate and particle size.
    /// 1.0 = normal.
    /// </summary>
    [DataField]
    public float Intensity = 1f;

    /// <summary>
    /// Local-space offset applied to the spawn origin of this emitter.
    /// Useful for shifting the effect away from the entity's anchor point.
    /// </summary>
    [DataField]
    public Vector2 SpawnOffset;
}
