using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeExtractEnhancerPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedPopupSystem _sharedPopupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeExtractEnhancerPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SlimeExtractEnhancerPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        args.Handled = true;
        if (!_entityManager.TryGetComponent<SlimeExtractComponent>(args.Target.Value,
                out var slimeExtractComponent)) return;
        slimeExtractComponent.RemainingUses += 1;
        var plural = slimeExtractComponent.RemainingUses == 1 ? "s" : "";
        _sharedPopupSystem.PopupPredicted($"{MetaData(args.Target.Value).EntityName} now has {slimeExtractComponent.RemainingUses} use{plural} remaining.", args.User, args.User);
        PredictedQueueDel(args.Used);
    }
}
