using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Containers.ItemSlots;

/// <summary>
/// Lets a held entity fill one of its own item slots by clicking the item to be inserted,
/// rather than requiring the holder to click the slot owner with that item.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ItemSlotQuickInsertComponent : Component
{
    [DataField(required: true)]
    public string Slot = string.Empty;
}
