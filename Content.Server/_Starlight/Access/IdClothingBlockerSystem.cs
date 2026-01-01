using Content.Server.Popups;
using Content.Shared._Starlight.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;

namespace Content.Server._Starlight.Access;

public sealed class IdClothingBlockerSystem : SharedIdClothingBlockerSystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedIdCardSystem _card = default!;
    
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, DidEquipHandEvent>(OnAnyHandEquipped);
        SubscribeLocalEvent<HandsComponent, DidUnequipHandEvent>(OnAnyHandUnequipped);
        SubscribeLocalEvent<InventoryComponent, DidEquipEvent>(OnAnyInventoryEquipped);
        SubscribeLocalEvent<InventoryComponent, DidUnequipEvent>(OnAnyInventoryUnequipped);
    }

    protected override void OnUnauthorizedAccess(EntityUid clothingUid, IdClothingBlockerComponent component, EntityUid wearer)
    {
        if (component.FreezeUser)
        {
            var blockedComponent = EntityManager.EnsureComponent<IdClothingFrozenComponent>(wearer);
            blockedComponent.ClothingItem = clothingUid;
            Dirty(wearer, blockedComponent);
        }

        UpdateClothingBlockingState(wearer);

        _popup.PopupEntity(Loc.GetString("access-clothing-blocker-notify-unauthorized-access"), clothingUid, PopupType.MediumCaution);
    }

    public void SetBlocked(EntityUid uid, IdClothingBlockerComponent component, bool blocked)
    {
        component.IsBlocked = blocked;
        Dirty(uid, component);
    }

    protected override void PopupClient(string message, EntityUid uid, EntityUid? target = null)
    {
        if (target.HasValue)
        {
            _popup.PopupEntity(message, uid, target.Value, PopupType.MediumCaution);
        }
    }

    protected override void OnUnequipAttempt(EntityUid uid, IdClothingBlockerComponent component, BeingUnequippedAttemptEvent args)
    {
        var wearerHasAccess = HasJobAccess(args.Unequipee, component);
        if (wearerHasAccess)
            return;

        if (args.UnEquipTarget == args.Unequipee)
        {
            args.Cancel();
        }
    }

    protected override void OnUnequipDoAfterAttempt(EntityUid uid, IdClothingBlockerComponent component, DoAfterAttemptEvent<ClothingUnequipDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target == null)
            return;

        var wearerHasAccess = HasJobAccess(args.DoAfter.Args.Target.Value, component);

        if (wearerHasAccess)
            return;

        args.Cancel();
        PopupClient(Loc.GetString("access-clothing-blocker-notify-unauthorized-access"), uid);
    }

    protected override bool HasJobAccess(EntityUid wearer, IdClothingBlockerComponent component)
    {
        if (component.AllowedJobs == null)
            return true;
        
        _card.TryFindIdCard(wearer, out var card);
        TryComp<AccessComponent>(card.Owner, out var access);
        return access != null && access.Tags.Overlaps(component.AllowedJobs);
    }

    // We assume access might have changed when a hand or inventory is equipped or unequipped
    private void OnAnyHandEquipped(EntityUid wearer, HandsComponent component, DidEquipHandEvent args)
    {
        UpdateClothingBlockingState(wearer);
    }

    private void OnAnyHandUnequipped(EntityUid wearer, HandsComponent component, DidUnequipHandEvent args)
    {
        UpdateClothingBlockingState(wearer);
    }

    private void OnAnyInventoryEquipped(EntityUid wearer, InventoryComponent component, DidEquipEvent args)
    {
        UpdateClothingBlockingState(wearer);
    }

    private void OnAnyInventoryUnequipped(EntityUid wearer, InventoryComponent component, DidUnequipEvent args)
    {
        UpdateClothingBlockingState(wearer);
    }

    private void UpdateClothingBlockingState(EntityUid wearer)
    {
        if (!TryComp<InventoryComponent>(wearer, out var inventory))
            return;

        foreach (var container in inventory.Containers)
        {
            var clothing = container.ContainedEntity;
            if (clothing == null)
                continue;

            if (!TryComp<IdClothingBlockerComponent>(clothing, out var blocker))
                continue;

            var hasAccess = HasJobAccess(wearer, blocker);
            SetBlocked(clothing.Value, blocker, !hasAccess);
        }
    }
}
