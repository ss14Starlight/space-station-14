using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;

namespace Content.Shared._Starlight.Containers.ItemSlots;

/// <summary>
/// Inserts a clicked entity into an item slot on the entity the user is holding.
/// </summary>
public sealed class ItemSlotQuickInsertSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSlotQuickInsertComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<ItemSlotQuickInsertComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!_itemSlots.TryGetSlot(ent, ent.Comp.Slot, out var slot))
            return;

        if (!_itemSlots.CanInsert(ent, target, args.User, slot))
            return;

        args.Handled = _itemSlots.TryInsert(ent, slot, target, args.User);
    }
}
