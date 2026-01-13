using Content.Shared.FixedPoint;
using Content.Shared.Interaction;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeMutationPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeMutationPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }
    
    private void OnAfterInteract(Entity<SlimeMutationPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        if (!_entityManager.TryGetComponent<SlimeComponent>(args.Target.Value,
                out var slimeComponent)) return;
        if (slimeComponent.MutationChance >= 1) return;
        slimeComponent.MutationChance = FixedPoint2.Min(1, slimeComponent.MutationChance + 0.12);
        PredictedQueueDel(args.Used);
        args.Handled = true;
    }
}