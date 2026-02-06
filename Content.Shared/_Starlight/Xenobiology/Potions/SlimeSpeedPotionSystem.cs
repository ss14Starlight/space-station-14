using Content.Shared.Clothing;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeSpeedPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly ClothingSpeedModifierSystem _clothingSpeedModifierSystem = default!;
    [Dependency] private readonly SharedPopupSystem _sharedPopupSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeSpeedPotionComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SlimeSpeedPotionComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
    }
    
    private void OnAfterInteract(Entity<SlimeSpeedPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        args.Handled = true;
        if (!TryModifyWalkSpeed(args.Target.Value, args.User)) return;
        PredictedQueueDel(args.Used);
    }

    private bool TryModifyWalkSpeed(EntityUid target, EntityUid user)
    {
        if (!_entityManager.TryGetComponent<ClothingSpeedModifierComponent>(target,
                out var clothingSpeedModifierComponent)) return false;
        _clothingSpeedModifierSystem.SetWalkSpeedModifier(clothingSpeedModifierComponent, (clothingSpeedModifierComponent.WalkModifier + 1.0F) / 2.0F);
        _clothingSpeedModifierSystem.SetSprintSpeedModifier(clothingSpeedModifierComponent, (clothingSpeedModifierComponent.SprintModifier + 1.0F) / 2.0F);
        Dirty(target, clothingSpeedModifierComponent);
        _sharedPopupSystem.PopupPredicted($"{MetaData(target).EntityName} walk/sprint speed reduction is now {clothingSpeedModifierComponent.WalkModifier}/{clothingSpeedModifierComponent.SprintModifier}.", user, user);
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
                if (TryModifyWalkSpeed(target, args.User))
                    PredictedQueueDel(uid);
            },
            Text = Loc.GetString("speed-potion-apply-text")
        };

        args.Verbs.Add(verb);
    }
}