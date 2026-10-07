using Robust.Shared.GameObjects;

namespace Content.Shared.Containers.ItemSlots;

public sealed partial class ItemSlotsSystem
{
    public void SuppressStartingItemsOnConstructionChange(EntityUid uid)
    {
        if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots) ||
            !itemSlots.SuppressStartingItemsOnConstructionChange)
        {
            return;
        }

        foreach (var slot in itemSlots.Slots.Values)
        {
            slot.StartingItem = null;
        }
    }
}
