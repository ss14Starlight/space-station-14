using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeMutationPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedPopupSystem _sharedPopupSystem = default!;

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
            _sharedPopupSystem.PopupPredicted($"{MetaData(args.Target.Value).EntityName} is already at 100% mutation chance. Cannot raise higher.", args.User, args.User);
            return;
        }
        slimeComponent.MutationChance = FixedPoint2.Min(1, slimeComponent.MutationChance + SlimeMutationPotionComponent.MutationChangeAmount);
        _sharedPopupSystem.PopupPredicted($"{MetaData(args.Target.Value).EntityName} now has a {slimeComponent.MutationChance * 100}% chance of mutating.", args.User, args.User);
        PredictedQueueDel(args.Used);
    }
}
