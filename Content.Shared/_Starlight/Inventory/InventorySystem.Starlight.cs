using Content.Shared.Hands.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Inventory;

public abstract partial class InventorySystem
{
    /// <summary>
    /// Without hands, an equipped item can't be taken off into a hand, so clicking it activates it instead
    /// (e.g. a K9 opening its collar).
    /// </summary>
    private bool TryActivateSlotWithoutHands(EntityUid actor, string slot)
    {
        if (HasComp<HandsComponent>(actor))
            return false;

        if (TryGetSlotEntity(actor, slot, out var item))
            _interactionSystem.InteractionActivate(actor, item.Value);

        return true;
    }
}
