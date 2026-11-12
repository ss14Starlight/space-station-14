using Content.Shared._Moffstation.Clothing.ModularHud.Components;
using Content.Shared._Moffstation.Clothing.ModularHud.Systems;
using Content.Shared._Starlight.Overlay.Components;
using Content.Shared._Starlight.Overlay.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Player;

namespace Content.Shared._Starlight.Overlay.Systems;

public sealed partial class FlashImmunitySystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedFlashSystem _flash = default!;
    [Dependency] private SharedModularHudSystem _modularHud = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TransformComponent, FlashImmunityChangedEvent>(OnModularHudFlashImmunityChanged);

        SubscribeLocalEvent<FlashImmunityComponent, GotEquippedEvent>(OnFlashImmunityEquipped);
        SubscribeLocalEvent<FlashImmunityComponent, GotUnequippedEvent>(OnFlashImmunityUnEquipped);

        SubscribeLocalEvent<FlashImmunityComponent, ComponentStartup>(OnFlashImmunityChanged);
        SubscribeLocalEvent<FlashImmunityComponent, ComponentRemove>(OnFlashImmunityChanged);

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);

        SubscribeLocalEvent<NightVisionComponent, ComponentStartup>(OnVisionChanged);
        SubscribeLocalEvent<NightVisionComponent, ComponentRemove>(OnVisionChanged); //this should use shutdown, but something else is already using it.....

        SubscribeLocalEvent<ThermalVisionComponent, ComponentStartup>(OnVisionChanged);
        SubscribeLocalEvent<ThermalVisionComponent, ComponentRemove>(OnVisionChanged); //this should use shutdown, but something else is already using it.....

        SubscribeLocalEvent<CycloriteVisionComponent, ComponentStartup>(OnVisionChanged);
        SubscribeLocalEvent<CycloriteVisionComponent, ComponentRemove>(OnVisionChanged); //this should use shutdown, but something else is already using it.....
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        FlashImmunityCheckEvent flashImmunityChangedEvent = new(args.Entity, HasFlashImmunityVisionBlockers(args.Entity));
        RaiseLocalEvent(args.Entity, flashImmunityChangedEvent);
    }

    private void OnFlashImmunityChanged(EntityUid uid, FlashImmunityComponent component, EntityEventArgs args)
    {
        var ev = new FlashImmunityChangedEvent(_flash.IsFlashImmune(uid));
        RaiseLocalEvent(uid, ref ev);

        uid = GetPossibleWearer(uid);
        FlashImmunityCheckEvent flashImmunityChangedEvent = new(uid, HasFlashImmunityVisionBlockers(uid));
        RaiseLocalEvent(uid, flashImmunityChangedEvent);
    }

    private void OnVisionChanged(EntityUid uid, Component component, EntityEventArgs args)
    {
        uid = GetPossibleWearer(uid);
        FlashImmunityCheckEvent flashImmunityChangedEvent = new(uid, HasFlashImmunityVisionBlockers(uid));
        RaiseLocalEvent(uid, flashImmunityChangedEvent);
    }

    private void OnFlashImmunityEquipped(EntityUid uid, FlashImmunityComponent component, GotEquippedEvent args)
    {
        var ev = new FlashImmunityChangedEvent(_flash.IsFlashImmune(args.EquipTarget));
        RaiseLocalEvent(args.EquipTarget, ref ev);

        FlashImmunityCheckEvent flashImmunityChangedEvent = new(uid, HasFlashImmunityVisionBlockers(args.EquipTarget));
        RaiseLocalEvent(args.EquipTarget, flashImmunityChangedEvent);
    }

    private void OnFlashImmunityUnEquipped(EntityUid uid, FlashImmunityComponent component, GotUnequippedEvent args)
    {
        var ev = new FlashImmunityChangedEvent(_flash.IsFlashImmune(args.EquipTarget));
        RaiseLocalEvent(args.EquipTarget, ref ev);

        FlashImmunityCheckEvent flashImmunityChangedEvent = new(uid, HasFlashImmunityVisionBlockers(args.EquipTarget));
        RaiseLocalEvent(args.EquipTarget, flashImmunityChangedEvent);
    }

    private EntityUid GetPossibleWearer(EntityUid uid)
    {
        if (TryComp<ClothingComponent>(uid, out var clothingComponent))
        {
            //we want to get the wearer of the clothing, not the clothing itself
            return Transform(uid).ParentUid;
        }

        return uid;
    }

    public bool HasFlashImmunityVisionBlockers(EntityUid uid)
    {
        if (TryComp(uid, out FlashImmunityComponent? flashImmunityComponent))
        {
            if (flashImmunityComponent.BlocksSpecialVision)
                return true;
        }

        if (TryComp<InventoryComponent>(uid, out var inventoryComp))
        {
            //get all worn items
            var slots = _inventory.GetSlotEnumerator((uid, inventoryComp), SlotFlags.WITHOUT_POCKET);
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is { } item && HasModularHudVisionBlockers(item))
                    return true;

                if (slot.ContainedEntity != null && TryComp(slot.ContainedEntity, out FlashImmunityComponent? wornFlashImmunityComponent))
                {
                    if (wornFlashImmunityComponent.BlocksSpecialVision)
                        return true;
                }
            }
        }

        return false;
    }

    private void OnModularHudFlashImmunityChanged(Entity<TransformComponent> ent, ref FlashImmunityChangedEvent args)
    {
        var uid = GetPossibleWearer(ent.Owner);
        var ev = new FlashImmunityCheckEvent(uid, HasFlashImmunityVisionBlockers(uid));
        RaiseLocalEvent(uid, ev);
    }

    private bool HasModularHudVisionBlockers(EntityUid uid)
    {
        if (!TryComp<ModularHudComponent>(uid, out var hud) ||
            !_inventory.InSlotWithAnyFlags(uid, hud.ActiveSlots))
            return false;

        foreach (var module in _modularHud.GetModules((uid, hud)))
        {
            if (TryComp<FlashImmunityComponent>(module, out var immunity) && immunity.BlocksSpecialVision)
                return true;
        }

        return false;
    }
}
