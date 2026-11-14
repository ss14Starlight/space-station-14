using Content.Shared._Starlight.Reptilian.Components;
using Content.Shared.GameTicking;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Reptilian;

/// <summary>
/// When a thick-scaled player spawns, swaps starting gear they can't use (like emergency medipens)
/// for the replacements listed in <see cref="ThickScalesComponent.StartingGearReplacements"/>.
/// </summary>
public sealed partial class ThickScalesGearSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [SubscribeLocalEvent]
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!TryComp<ThickScalesComponent>(args.Mob, out var scales) || scales.StartingGearReplacements.Count == 0)
            return;

        var found = new List<(EntityUid Item, EntProtoId Replacement)>();
        CollectReplacements(args.Mob, scales, found);

        foreach (var (item, replacement) in found)
            Replace(item, replacement);
    }

    /// <summary>
    /// Walks every container on <paramref name="uid"/> and the items inside them, recursively, and adds
    /// each item that has a replacement to <paramref name="found"/>. Replaced items aren't searched further.
    /// </summary>
    private void CollectReplacements(EntityUid uid, ThickScalesComponent scales, List<(EntityUid, EntProtoId)> found)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in _container.GetAllContainers(uid, manager))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (MetaData(contained).EntityPrototype?.ID is { } protoId
                    && scales.StartingGearReplacements.TryGetValue(protoId, out var replacement))
                {
                    found.Add((contained, replacement));
                    continue;
                }

                CollectReplacements(contained, scales, found);
            }
        }
    }

    private void Replace(EntityUid item, EntProtoId replacement)
    {
        if (!_container.TryGetContainingContainer(item, out var container))
            return;

        var owner = container.Owner;
        var coords = _transform.GetMapCoordinates(owner);
        Del(item);

        // Spawned loose on the map, so a failed insert below leaves it on the floor, not parented to carried gear.
        var newItem = Spawn(replacement, coords);
        if (TryComp<StorageComponent>(owner, out var storage) && container.ID == StorageComponent.ContainerId)
        {
            if (_storage.Insert(owner, newItem, out _, storageComp: storage, playSound: false))
                return;
        }
        else if (_container.Insert(newItem, container))
        {
            return;
        }

        // Nowhere to put it: drop it at the wearer's feet rather than losing it.
        _transform.AttachToGridOrMap(newItem);
    }
}
