using Content.Shared.Item;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Containers;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    private readonly Dictionary<Vector2i, ulong> _resizeIgnored = [];
    private readonly List<Box2i> _resizeShape = [];

    private void RelocateResizedItem(Entity<ItemComponent?> itemEnt, BaseContainer container, StorageComponent storage, ItemStorageLocation loc)
    {
        var storageEnt = new Entity<StorageComponent>(container.Owner, storage);
        storage.StoredItems.Remove(itemEnt.Owner);
        UpdateOccupied(storageEnt);

        _resizeIgnored.Clear();
        if (TryFindResizedLocation(storageEnt, ItemSystem.GetItemShape(itemEnt), loc, _resizeIgnored, out var newLoc))
        {
            storage.StoredItems[itemEnt.Owner] = newLoc;
            AddOccupiedEntity(storageEnt, itemEnt, newLoc);
            UpdateUI((storageEnt.Owner, storage));
            return;
        }

        ContainerSystem.Remove(itemEnt.Owner, container, force: true);
    }

    [SubscribeLocalEvent]
    private void OnToggleSizeActivateAttempt(Entity<ItemToggleSizeComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (CanResizeInStorage(ent, activated: true, out var reason))
            return;

        args.Cancelled = true;
        args.Popup = Loc.GetString(reason);
    }

    private bool CanResizeInStorage(Entity<ItemToggleSizeComponent> ent, bool activated, out string reason)
    {
        reason = "comp-storage-insufficient-capacity";

        var itemEnt = new Entity<ItemComponent?>(ent.Owner, null);
        if (!TryGetStorageLocation(itemEnt, out var container, out var storage, out var loc))
            return true;

        if (!ItemSystem.TryGetToggledShape((ent.Owner, ent.Comp, itemEnt.Comp), activated, out var newSize, out var newShape))
            return true;

        var storageEnt = new Entity<StorageComponent>(container.Owner, storage);

        if (ItemSystem.GetSizePrototype(newSize) > GetMaxItemSize((storageEnt.Owner, storage)))
        {
            reason = "comp-storage-too-big";
            return false;
        }

        _resizeIgnored.Clear();
        AddOccupied(itemEnt, loc, _resizeIgnored);

        return TryFindResizedLocation(storageEnt, newShape, loc, _resizeIgnored, out _);
    }

    private bool TryFindResizedLocation(
        Entity<StorageComponent> storageEnt,
        IReadOnlyList<Box2i> shape,
        ItemStorageLocation current,
        Dictionary<Vector2i, ulong> ignored,
        out ItemStorageLocation location)
    {
        location = current;

        _resizeShape.Clear();
        SharedItemSystem.GetAdjustedItemShape(_resizeShape, shape, current.Rotation, current.Position);
        if (_resizeShape.Count == 0)
            return false;

        if (ItemFitsInGridLocation(storageEnt.Comp.OccupiedGrid, _resizeShape, ignored))
            return true;

        // Snap the item back inside the storage bounds.
        var grid = storageEnt.Comp.Grid.GetBoundingBox();
        var bounds = _resizeShape.GetBoundingBox();

        if (bounds.Width > grid.Width || bounds.Height > grid.Height)
            return false;

        var offsetX = 0;
        var offsetY = 0;

        if (bounds.Right > grid.Right)
            offsetX = grid.Right - bounds.Right;
        else if (bounds.Left < grid.Left)
            offsetX = grid.Left - bounds.Left;

        if (bounds.Top > grid.Top)
            offsetY = grid.Top - bounds.Top;
        else if (bounds.Bottom < grid.Bottom)
            offsetY = grid.Bottom - bounds.Bottom;

        if (offsetX == 0 && offsetY == 0)
            return false;

        var offset = new Vector2i(offsetX, offsetY);

        for (var i = 0; i < _resizeShape.Count; i++)
        {
            _resizeShape[i] = _resizeShape[i].Translated(offset);
        }

        if (!ItemFitsInGridLocation(storageEnt.Comp.OccupiedGrid, _resizeShape, ignored))
            return false;

        location = new ItemStorageLocation(current.Rotation, current.Position + offset);
        return true;
    }
}
