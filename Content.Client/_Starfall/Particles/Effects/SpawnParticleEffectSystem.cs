using Content.Shared._Starfall.Particles.Effects;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Client._Starfall.Particles.Effects;

/// <summary>
/// Executes <see cref="SpawnParticleEffect"/> on the client, spawning a particle effect on the target entity.
/// </summary>
public sealed partial class SpawnParticleEffectSystem : EntityEffectSystem<TransformComponent, SpawnParticleEffect>
{
    [Dependency] private readonly ParticleSystem _particles = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<SpawnParticleEffect> args)
    {
        if (!_proto.TryIndex(args.Effect.Effect, out var proto))
        {
            Log.Error($"SpawnParticleEffect references unknown particle effect '{args.Effect.Effect}'");
            return;
        }

        // Infinite-duration effects (Duration == 0, not burst, no timed bursts) can never be stopped and are not appropriate for this use case, which is meant for short-lived effects like hit sparks or explosion flashes.
        if (proto.Duration == TimeSpan.Zero && !proto.Burst && proto.Bursts.Count == 0)
        {
            Log.Error($"SpawnParticleEffect tried to spawn '{args.Effect.Effect}' which has infinite duration (Duration=0, Burst=false). " +
                      $"Set a finite Duration or use Burst mode.");
            return;
        }

        _particles.CreateParticle(args.Effect.Effect, entity.Owner, args.Effect.ColorOverride);
    }
}

