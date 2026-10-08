using Content.Shared.Containers.ItemSlots;

// ReSharper disable once CheckNamespace
namespace Content.Server.Construction;

public sealed partial class ConstructionSystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    [SubscribeLocalEvent]
    private void OnConstructionChangeEntity(ConstructionChangeEntityEvent args) =>
        _itemSlots.SuppressStartingItemsOnConstructionChange(new(args.New, null));
}
