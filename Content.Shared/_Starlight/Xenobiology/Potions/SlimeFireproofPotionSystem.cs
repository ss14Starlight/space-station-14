using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeFireproofPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeFireproofPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SlimeFireproofPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        if (!_entityManager.TryGetComponent<DamageableComponent>(args.Target.Value,
                out _)) return;
        if (!_prototypeManager.Resolve(ent.Comp.FireproofDamageSet, out var modifier))
            return;
        var damageProtectionBuffComponent = _entityManager.EnsureComponent<DamageProtectionBuffComponent>(args.Target.Value);
        damageProtectionBuffComponent.Modifiers.TryAdd("SlimeFireproofPotionEffect", modifier);
        ent.Comp.RemainingUses -= 1;
        if (ent.Comp.RemainingUses <= 0)
            PredictedQueueDel(args.Used);
        args.Handled = true;
    }
}