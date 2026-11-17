using Content.Shared.Inventory;
using Robust.Client.GameObjects;

// ReSharper disable CheckNamespace
namespace Content.Client.Clothing;

public sealed partial class ClientClothingSystem
{
    /// <summary>
    /// Make use of the layer map reserve method to ensure consistent layering order when joining after a disconnect.
    /// </summary>
    private void ReserveSlots(Entity<InventoryComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        foreach (var slot in ent.Comp.Slots)
        {
            if (!_sprite.LayerMapTryGet((ent, sprite), slot.Name, out _, false))
                _sprite.LayerMapReserve((ent, sprite), slot.Name);
        }
    }
}
