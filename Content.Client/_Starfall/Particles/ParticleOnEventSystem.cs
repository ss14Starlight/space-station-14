using Content.Shared._Starfall.Particles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Client._Starfall.Particles;

/// <summary>
/// Handles the particle onevent cases that (from as far as I am aware) cannot be replaced by <see cref="SpawnParticleEffect"/> and the trigger system.
/// <see cref="ParticleOnThrownComponent"/>, continuous emission while in flight, the active emitter must be tracked and explicitly stopped on landing.</item>
/// <see cref="ParticleOnGunShotProjectileComponent"/> attaches a particle emitter to
/// every individual projectile fired, which has no trigger equivalent,
/// although you should put this on the projectile entity itself as an emitter ideally.</item>
/// All other event particle needs should use <see cref="SpawnParticleEffect"/> in <c>EntityEffectOnTrigger</c> component instead of dedicated components here.
/// </summary>
public sealed class ParticleOnEventSystem : EntitySystem
{
    [Dependency] private readonly ParticleSystem _particles = default!;

    // Track emitters spawned by OnThrown so we can stop them when the entity lands
    private readonly Dictionary<EntityUid, ActiveEmitter> _thrownEmitters = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ParticleOnThrownComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<ParticleOnThrownComponent, LandEvent>(OnThrownLanded);
        SubscribeLocalEvent<ParticleOnThrownComponent, ComponentShutdown>(OnThrownShutdown);

        SubscribeLocalEvent<ParticleOnGunShotProjectileComponent, AmmoShotEvent>(OnGunShotProjectile);
    }

    private void OnThrown(Entity<ParticleOnThrownComponent> ent, ref ThrownEvent args)
    {
        // Stop any existing emitter first to avoid orphaning it on a re-throw.
        StopThrownEmitter(ent.Owner);

        // Infinite-duration allowed: the emitter is stopped when the entity lands.
        var emitter = _particles.CreateParticle(ent.Comp.Effect, ent.Owner, ent.Comp.ColorOverride);
        if (emitter != null)
            _thrownEmitters[ent.Owner] = emitter;
    }

    private void OnThrownLanded(Entity<ParticleOnThrownComponent> ent, ref LandEvent args)
    {
        StopThrownEmitter(ent.Owner);
    }

    private void OnThrownShutdown(Entity<ParticleOnThrownComponent> ent, ref ComponentShutdown args)
    {
        StopThrownEmitter(ent.Owner);
    }

    private void StopThrownEmitter(EntityUid uid)
    {
        if (_thrownEmitters.Remove(uid, out var emitter))
            _particles.RemoveParticle(emitter);
    }

    private void OnGunShotProjectile(Entity<ParticleOnGunShotProjectileComponent> ent, ref AmmoShotEvent args)
    {
        // Infinite-duration allowed: the emitter is cleaned up when the projectile is destroyed.
        foreach (var projectile in args.FiredProjectiles)
        {
            _particles.CreateParticle(ent.Comp.Effect, projectile, ent.Comp.ColorOverride);
        }
    }
}
