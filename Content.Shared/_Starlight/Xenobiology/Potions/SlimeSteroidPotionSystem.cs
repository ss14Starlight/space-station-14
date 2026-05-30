using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeSteroidPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedPopupSystem _sharedPopupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeSteroidPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SlimeSteroidPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        args.Handled = true;
        if (!_entityManager.TryGetComponent<SlimeComponent>(args.Target.Value,
                out var slimeComponent)) return;
        slimeComponent.SlimeSteroidAmount += 1;
        var plural = slimeComponent.SlimeSteroidAmount == 1 ? "" : "s";
        _sharedPopupSystem.PopupPredicted($"{MetaData(args.Target.Value).EntityName} now creates {slimeComponent.SlimeSteroidAmount} extra extract{plural} when processed.", args.User, args.User);
        PredictedQueueDel(args.Used);
    }
}
