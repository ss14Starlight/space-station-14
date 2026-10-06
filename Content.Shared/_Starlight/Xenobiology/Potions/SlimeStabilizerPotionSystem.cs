using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed partial class SlimeStabilizerPotionSystem : EntitySystem
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private SharedPopupSystem _sharedPopupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeStabilizerPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SlimeStabilizerPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        args.Handled = true;
        if (!_entityManager.TryGetComponent<SlimeComponent>(args.Target.Value,
                out var slimeComponent)) return;
        if (slimeComponent.MutationChance <= 0)
        {
            _sharedPopupSystem.PopupPredicted(Loc.GetString("slime-potion-mutation-min", ("target", MetaData(args.Target.Value).EntityName)), args.User, args.User);
            return;
        }
        slimeComponent.MutationChance = FixedPoint2.Max(0, slimeComponent.MutationChance + SlimeStabilizerPotionComponent.MutationChangeAmount);
        _sharedPopupSystem.PopupPredicted(Loc.GetString("slime-potion-mutation-chance", ("target", MetaData(args.Target.Value).EntityName), ("chance", slimeComponent.MutationChance * 100)), args.User, args.User);
        PredictedQueueDel(args.Used);
    }
}
