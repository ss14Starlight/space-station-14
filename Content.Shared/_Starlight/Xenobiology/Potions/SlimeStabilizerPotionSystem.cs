using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeStabilizerPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedPopupSystem _sharedPopupSystem = default!;
    
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
            _sharedPopupSystem.PopupPredicted($"{MetaData(args.Target.Value).EntityName} is already at 0% mutation chance. Cannot lower further.", args.User, args.User);
            return;
        }
        slimeComponent.MutationChance = FixedPoint2.Max(0, slimeComponent.MutationChance + SlimeStabilizerPotionComponent.MutationChangeAmount);
        _sharedPopupSystem.PopupPredicted($"{MetaData(args.Target.Value).EntityName} now has a {slimeComponent.MutationChance * 100}% chance of mutating.", args.User, args.User);
        PredictedQueueDel(args.Used);
    }
}