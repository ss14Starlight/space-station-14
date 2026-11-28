using System.Linq;
using Content.Shared.CardboardBox.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mobs.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Network;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Vehicle.Systems;

public sealed partial class VehicleSystem
{
    [Dependency] private INetManager _net = default!;

    [SubscribeLocalEvent]
    private void OnVehicleTerminating(Entity<VehicleComponent> ent, ref EntityTerminatingEvent _)
    {
        if (_net.IsClient)
            return;

        // don't spill things out if you're deleting the entire grid
        var xform = Transform(ent);
        if (!xform.ParentUid.IsValid() || TerminatingOrDeleted(xform.ParentUid) ||
            xform.MapUid is not { } map || TerminatingOrDeleted(map) ||
            (xform.GridUid is { } grid && TerminatingOrDeleted(grid)))
            return;

        if (TryGetOperatorContainer(ent.Owner, out var cabin))
        {
            // cardboard boxes don't always have people in em
            if (HasComp<CardboardBoxComponent>(ent) &&
                !cabin.ContainedEntities.Any(occupant => HasComp<MobStateComponent>(occupant)))
                return;

            _container.EmptyContainer(cabin, force: true);
        }

        if (TryComp<EntityStorageComponent>(ent, out var trunk) && trunk.Contents is { } contents)
            _container.EmptyContainer(contents, force: true);

        if (TryComp<StorageComponent>(ent, out var storage) && storage.Container is { } cargo)
            _container.EmptyContainer(cargo, force: true);

        if (!TryComp<ItemSlotsComponent>(ent, out var slots))
            return;

        var keySlotId = GetVehicleKeySlot(ent.Owner);
        var cellSlotId = CompOrNull<PowerCellSlotComponent>(ent)?.CellSlotId;
        foreach (var slot in slots.Slots.Values.ToArray())
        {
            if (slot.Item is { } item &&
                slot.ContainerSlot is { } container &&
                container.ID != keySlotId &&
                container.ID != cellSlotId)
            {
                _container.Remove(item, container, force: true);
            }
        }
    }
}
