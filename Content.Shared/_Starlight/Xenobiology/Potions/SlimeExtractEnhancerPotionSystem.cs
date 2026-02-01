using Content.Shared.Interaction;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeExtractEnhancerPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeExtractEnhancerPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }
    
    private void OnAfterInteract(Entity<SlimeExtractEnhancerPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        if (!_entityManager.TryGetComponent<SlimeExtractComponent>(args.Target.Value,
                out var slimeExtractComponent)) return;
        slimeExtractComponent.RemainingUses += 1;
        PredictedQueueDel(args.Used);
        args.Handled = true;
    }
}