using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Starlight.Inventory;

/// <inheritdoc cref="AnimalCollarComponent"/>
public sealed partial class AnimalCollarSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    [SubscribeLocalEvent]
    private void OnBeingEquippedAttempt(Entity<AnimalCollarComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled
            || (_inventory.TryGetSlot(args.EquipTarget, args.Slot, out var slot)
                && slot.Whitelist?.Tags?.Contains(ent.Comp.SlotTag) == true))
            return;

        args.Reason = "inventory-component-can-equip-does-not-fit";
        args.Cancel();
    }
}
