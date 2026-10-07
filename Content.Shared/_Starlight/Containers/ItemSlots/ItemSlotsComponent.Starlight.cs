using Robust.Shared.GameObjects;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Containers.ItemSlots;

public sealed partial class ItemSlotsComponent
{
    /// <summary>
    /// Whether configured starting items should be suppressed when construction creates this entity.
    /// </summary>
    [DataField]
    public bool SuppressStartingItemsOnConstructionChange;
}
