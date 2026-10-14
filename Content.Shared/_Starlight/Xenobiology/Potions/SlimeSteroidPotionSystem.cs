using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed partial class SlimeSteroidPotionSystem : EntitySystem
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private SharedPopupSystem _sharedPopupSystem = default!;

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
        _sharedPopupSystem.PopupPredicted(Loc.GetString("slime-potion-steroid-applied", ("target", MetaData(args.Target.Value).EntityName), ("amount", slimeComponent.SlimeSteroidAmount)), args.User, args.User);
        PredictedQueueDel(args.Used);
    }
}
