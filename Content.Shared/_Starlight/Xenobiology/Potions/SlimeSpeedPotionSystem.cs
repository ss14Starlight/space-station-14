using Content.Shared.Clothing;
using Content.Shared.Interaction;
using Content.Shared.Verbs;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeSpeedPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly ClothingSpeedModifierSystem _clothingSpeedModifierSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeSpeedPotionComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SlimeSpeedPotionComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
    }
    
    private void OnAfterInteract(Entity<SlimeSpeedPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        if (!TryModifyWalkSpeed(args.Target.Value)) return;
        PredictedQueueDel(args.Used);
        args.Handled = true;
    }

    private bool TryModifyWalkSpeed(EntityUid target)
    {
        if (!_entityManager.TryGetComponent<ClothingSpeedModifierComponent>(target,
                out var clothingSpeedModifierComponent)) return false;
        _clothingSpeedModifierSystem.SetWalkSpeedModifier(clothingSpeedModifierComponent, (clothingSpeedModifierComponent.WalkModifier + 1.0F) / 2.0F);
        _clothingSpeedModifierSystem.SetSprintSpeedModifier(clothingSpeedModifierComponent, (clothingSpeedModifierComponent.SprintModifier + 1.0F) / 2.0F);
        return true;
    }

    private void OnUtilityVerb(EntityUid uid, SlimeSpeedPotionComponent slimeSpeedPotionComponent, GetVerbsEvent<UtilityVerb> args)
    {
        if (args.Target is not { Valid: true } target || !args.CanAccess)
            return;
        
        var verb = new UtilityVerb()
        {
            Act = () =>
            {
                if (TryModifyWalkSpeed(target))
                    PredictedQueueDel(uid);
            },
            Text = Loc.GetString("speed-potion-apply-text")
        };

        args.Verbs.Add(verb);
    }
}