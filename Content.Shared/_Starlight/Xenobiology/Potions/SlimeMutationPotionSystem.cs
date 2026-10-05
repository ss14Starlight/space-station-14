using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed partial class SlimeMutationPotionSystem : EntitySystem
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private SharedPopupSystem _sharedPopupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeMutationPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SlimeMutationPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        args.Handled = true;
        if (!_entityManager.TryGetComponent<SlimeComponent>(args.Target.Value,
                out var slimeComponent)) return;
        if (slimeComponent.MutationChance >= 1)
        {
            _sharedPopupSystem.PopupPredicted(Loc.GetString("slime-potion-mutation-max", ("target", MetaData(args.Target.Value).EntityName)), args.User, args.User);
            return;
        }
        slimeComponent.MutationChance = FixedPoint2.Min(1, slimeComponent.MutationChance + SlimeMutationPotionComponent.MutationChangeAmount);
        _sharedPopupSystem.PopupPredicted(Loc.GetString("slime-potion-mutation-chance", ("target", MetaData(args.Target.Value).EntityName), ("chance", slimeComponent.MutationChance * 100)), args.User, args.User);
        PredictedQueueDel(args.Used);
    }
}
