using Content.Shared.Interaction;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeSteroidPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeSteroidPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }
    
    private void OnAfterInteract(Entity<SlimeSteroidPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        if (!_entityManager.TryGetComponent<SlimeComponent>(args.Target.Value,
                out var slimeComponent)) return;
        if (slimeComponent.MutationChance >= 0) return;
        slimeComponent.SlimeSteroidAmount += 1;
        PredictedQueueDel(args.Used);
        args.Handled = true;
    }
}