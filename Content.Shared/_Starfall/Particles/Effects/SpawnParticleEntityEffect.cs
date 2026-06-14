using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starfall.Particles.Effects;

/// <summary>
/// An entity effect that spawns a particle effect on the target entity.
/// This only activates on the client.
/// </summary>
/// <remarks>
/// Add this to any <c>EntityEffect[]</c> list (melee weapon effects, trigger effects,
/// chemical reaction effects, etc.) instead of adding a dedicated
/// <c>ParticleOn*Component</c> for every single event type.
/// </remarks>
public sealed partial class SpawnParticleEffect : EntityEffectBase<SpawnParticleEffect>
{
    /// <summary>
    /// The particle effect prototype to spawn
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ParticleEffectPrototype> Effect;

    /// <summary>
    /// Optional color override.
    /// </summary>
    [DataField]
    public Color? ColorOverride;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null; // this doesnt need a guidebook entry
}

