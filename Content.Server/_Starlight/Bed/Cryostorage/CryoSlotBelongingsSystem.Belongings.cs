using Content.Shared.Access.Components;
using Content.Shared.Database;
using Content.Shared.PDA;
using Content.Shared.Storage;

namespace Content.Server._Starlight.Bed.Cryostorage;

public sealed partial class CryoSlotBelongingsSystem
{
    // grab top level worn and held items, nested stuff rides along with its container
    private List<EntityUid> CollectCryoItems(EntityUid body)
    {
        var items = new List<EntityUid>();

        var enumerator = _inventory.GetSlotEnumerator(body);
        while (enumerator.NextItem(out var item, out _))
        {
            if (ShouldKeep(item))
                items.Add(item);
        }

        foreach (var hand in _hands.EnumerateHands(body))
        {
            if (_hands.TryGetHeldItem(body, hand, out var held) && ShouldKeep(held.Value))
                items.Add(held.Value);
        }

        return items;
    }

    // pdas and id cards stay on the body, no point handing out someone else's access
    private bool ShouldKeep(EntityUid item)
        => !HasComp<PdaComponent>(item) && !HasComp<IdCardComponent>(item);

    // drop anything that got deleted or pulled off the body since we snapshotted it
    private List<EntityUid> GetValidItems(CryoLoadout loadout)
    {
        var valid = new List<EntityUid>();
        foreach (var item in loadout.Items)
        {
            if (Deleted(item) || !IsContainedBy(item, loadout.Body))
                continue;

            valid.Add(item);
        }

        return valid;
    }

    // true if item is the body itself or sits somewhere under it
    private bool IsContainedBy(EntityUid item, EntityUid body)
    {
        var parent = item;
        var safety = 0;
        while (parent.IsValid() && safety++ < 32)
        {
            if (parent == body)
                return true;

            parent = Transform(parent).ParentUid;
        }

        return false;
    }

    private void GiveCryoBag(EntityUid mob, CryoLoadout loadout, List<EntityUid> items)
    {
        // force the new crew member to spawn at the pod
        if (loadout.Pod is { } pod && !Deleted(pod))
            _transform.SetCoordinates(mob, Transform(pod).Coordinates);

        var bag = SpawnAtPosition(CryoBelongingsBag, Transform(mob).Coordinates);

        if (_container.TryGetContainer(bag, StorageComponent.ContainerId, out var container))
        {
            // force bypasses the read only insert guard on the bag
            foreach (var item in items)
                _container.Insert(item, container, force: true);
        }

        // hand it over, if both hands are full it just drops at their feet
        _hands.TryPickupAnyHand(mob, bag);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(mob):player} late joined into a cryo vacated slot and got {ToPrettyString(bag)} with {items.Count} preserved item(s).");
    }
}
