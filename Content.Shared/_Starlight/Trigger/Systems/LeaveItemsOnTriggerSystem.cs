using Content.Shared._Starlight.Trigger.Components.Effects;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Maps;
using Content.Shared.Storage;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.Trigger.Systems;

public sealed partial class LeaveItemsOnTriggerSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LeaveItemsOnTriggerComponent, TriggerEvent>(OnTrigger, before: new[] { typeof(TriggerSystem) });
    }

    private void OnTrigger(Entity<LeaveItemsOnTriggerComponent> ent, ref TriggerEvent args)
    {
        // Server-only so client prediction doesn't strip clothing layers before entity deletion (avoids naked flash)
        if (!_net.IsServer)
            return;

        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;
        if (target is not { } targetUid)
            return;

        // Fold toggleable items (like hardsuit helmets) back into suits first
        ReturnAttachedClothingToParents(targetUid);

        var items = new List<EntityUid>();
        foreach (var item in _inventory.GetHandOrInventoryEntities(targetUid))
        {
            if (!HasComp<AttachedClothingComponent>(item) && !HasComp<VirtualItemComponent>(item))
                items.Add(item);
        }

        // Center on tile to align with the anchored bluespace portal effect, or fall back to mover coords if in space
        var coords = _transform.GetMoverCoordinates(targetUid);
        if (_turf.TryGetTileRef(coords, out var tile) && !_turf.IsSpace(tile.Value))
            coords = _turf.GetTileCenter(tile.Value);

        var storageEnt = Spawn(ent.Comp.Proto, coords);
        if (_container.TryGetContainer(storageEnt, StorageComponent.ContainerId, out var container))
        {
            // Force insert bypasses the ReadOnlyStorage restriction
            foreach (var item in items)
                _container.Insert(item, container, force: true);
        }

        args.Handled = true;
    }

    // Ensures attached clothing items like hardsuit helmets return into their parent container
    private void ReturnAttachedClothingToParents(EntityUid body)
    {
        var attachedItems = new List<EntityUid>();
        var enumerator = _inventory.GetSlotEnumerator(body);

        while (enumerator.NextItem(out var item, out _))
        {
            if (HasComp<AttachedClothingComponent>(item))
                attachedItems.Add(item);
        }

        foreach (var item in attachedItems)
        {
            if (!TryComp<AttachedClothingComponent>(item, out var attached))
                continue;

            var parent = attached.AttachedUid;
            if (Deleted(parent))
                continue;

            if (!TryComp<ToggleableClothingComponent>(parent, out var toggleable) || toggleable.Container == null)
                continue;

            if (toggleable.Container.ContainedEntity == item)
                continue;

            if (_inventory.TryUnequip(body, toggleable.Slot, force: true))
                continue;

            _container.Insert(item, toggleable.Container, force: true);
        }
    }
}

