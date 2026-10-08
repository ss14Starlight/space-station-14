using Robust.Shared.GameObjects;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Containers.ItemSlots;

public sealed partial class ItemSlotsSystem
{
    /// <summary>
    /// Suppresses configured starting items when construction creates an entity.
    /// </summary>
    /// <param name="ent">The entity created by the construction change.</param>
    public void SuppressStartingItemsOnConstructionChange(Entity<ItemSlotsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (!ent.Comp.SuppressStartingItemsOnConstructionChange)
            return;

        foreach (var slot in ent.Comp.Slots.Values)
        {
            slot.StartingItem = null;
        }
    }
}
