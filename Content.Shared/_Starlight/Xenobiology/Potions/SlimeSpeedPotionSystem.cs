using Content.Shared.Clothing;
using Content.Shared.Interaction;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeSpeedPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly ClothingSpeedModifierSystem _clothingSpeedModifierSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeSpeedPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }
    
    private void OnAfterInteract(Entity<SlimeSpeedPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        if (!_entityManager.TryGetComponent<ClothingSpeedModifierComponent>(args.Target.Value,
                out var clothingSpeedModifierComponent)) return;
        _clothingSpeedModifierSystem.SetWalkSpeedModifier(clothingSpeedModifierComponent, (clothingSpeedModifierComponent.WalkModifier + 1.0F) / 2.0F);
        _clothingSpeedModifierSystem.SetSprintSpeedModifier(clothingSpeedModifierComponent, (clothingSpeedModifierComponent.SprintModifier + 1.0F) / 2.0F);
        PredictedQueueDel(args.Used);
        args.Handled = true;
    }
}