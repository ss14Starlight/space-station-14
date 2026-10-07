using Robust.Shared.GameObjects;

namespace Content.Shared.Containers.ItemSlots;

public sealed partial class ItemSlotsComponent
{
    /// <summary>
    /// Whether configured starting items should be suppressed when construction creates this entity.
    /// </summary>
    [DataField]
    public bool SuppressStartingItemsOnConstructionChange;
}
