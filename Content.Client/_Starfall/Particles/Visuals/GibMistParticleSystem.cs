using Content.Shared._Starfall.Particles;
using Robust.Shared.Prototypes;

namespace Content.Client._Starfall.Particles;

/// <summary>
/// Receives <see cref="GibMistParticleEvent"/> from the server and spawns
/// a blood-mist particle burst tinted to the entity's actual blood color.
/// </summary>
public sealed class GibMistParticleSystem : EntitySystem
{
    [Dependency] private readonly ParticleSystem _particles = default!;

    private static readonly ProtoId<ParticleEffectPrototype> MistEffect = "SfGibMist";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GibMistParticleEvent>(OnGibMist);
    }

    private void OnGibMist(GibMistParticleEvent ev)
    {
        var emitter = _particles.SpawnEffect(MistEffect, ev.Coords);
        if (emitter == null)
            return;

        emitter.ColorOverride = ev.BloodColor;
    }
}

