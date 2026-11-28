using System.Linq;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;

// ReSharper disable once CheckNamespace
namespace Content.Server.Resist;

public sealed partial class EscapeInventorySystem
{
    private void DropIfTrapped(EntityUid uid)
    {
        if (!_containerSystem.TryGetContainingContainer((uid, null, null), out var container)
            || CanEscapeFrom(uid, container))
            return;

        _containerSystem.Remove((uid, Transform(uid)), container, reparent: false);
        _transformSystem.AttachToGridOrMap(uid, Transform(uid));
    }

    private bool CanEscapeFrom(EntityUid uid, BaseContainer container)
    {
        var owner = container.Owner;

        return _handsSystem.IsHolding(owner, uid, out _)
            || (TryComp<StorageComponent>(owner, out var storage) && storage.Container == container)
            || (TryComp<InventoryComponent>(owner, out var inventory) && inventory.Containers.Any(slot => slot == container))
            || HasComp<SecretStashComponent>(owner)
            || _tagSystem.HasTag(owner, PersonnelStorageTag);
    }
}
