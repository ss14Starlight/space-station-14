using Content.Shared._Starlight.Genetics.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Genetics.Systems;

public sealed class PassiveTraitsSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffectsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PassiveTraitsComponent, EntityPausedEvent>(OnPaused);
        SubscribeLocalEvent<PassiveTraitsComponent, EntityUnpausedEvent>(OnUnpaused);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<Components.PassiveTraitsComponent>();
        while (query.MoveNext(out var uid, out var passiveTraitsComponent))
        {
            if (passiveTraitsComponent.Paused)
                continue;
            foreach (var (k, amt) in passiveTraitsComponent.Traits)
            {
                var timestampToActivate = passiveTraitsComponent.Cooldowns[k];
                if (timestampToActivate <= _gameTiming.CurTime)
                {
                    var proto = _prototypeManager.Index(k);
                    if (!proto.Threshold.HasValue || amt >= proto.Threshold.Value)
                    {
                        _entityEffectsSystem.TryApplyEffect(uid, proto.EntityEffect.Effect,
                            ((proto.EntityEffect.ScalingFactor * amt) + proto.EntityEffect.ScalingOffset).Float());
                    }
                    passiveTraitsComponent.Cooldowns[k] += proto.Cooldown;
                }
            }
        }
    }

    private void OnPaused(Entity<Components.PassiveTraitsComponent> entity, ref EntityPausedEvent args) => entity.Comp.Paused = true;

    private void OnUnpaused(Entity<Components.PassiveTraitsComponent> entity, ref EntityUnpausedEvent args)
    {
        entity.Comp.Paused = false;
        foreach (var k in entity.Comp.Traits.Keys)
        {
            entity.Comp.Cooldowns[k] += args.PausedTime;
        }
    }
}
