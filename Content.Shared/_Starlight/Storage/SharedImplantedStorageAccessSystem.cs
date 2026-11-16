using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Robust.Shared.Containers;
// ReSharper disable CheckNamespace

namespace Content.Shared._Starlight.Storage;

/// <summary>
/// Allows storage attached to visible body parts or custom limbs to be accessed from nearby.
/// Secret subdermal storage is deliberately excluded.
/// </summary>
public sealed partial class SharedImplantedStorageAccessSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    public IEnumerable<EntityUid> GetStrippableStorageEntities(EntityUid owner)
    {
        if (TryComp<StorageComponent>(owner, out var ownerStorage) &&
            HasComp<PrivateStorageComponent>(owner) &&
            CanShowImplantedStorage(owner, ownerStorage, bodyPart: false))
            yield return owner;

        if (!TryComp<BodyComponent>(owner, out var body))
            yield break;

        foreach (var (partUid, _) in _body.GetBodyChildren(owner, body))
        {
            if (!HasComp<StorageOrganComponent>(partUid) &&
                TryComp<StorageComponent>(partUid, out var storage) &&
                CanShowImplantedStorage(partUid, storage, bodyPart: true))
                yield return partUid;
        }
    }

    public IEnumerable<EntityUid> GetStrippableStorageHandEntities(EntityUid owner)
    {
        if (!TryComp<BodyComponent>(owner, out var body))
            yield break;

        foreach (var (partUid, _) in _body.GetBodyChildren(owner, body))
        {
            if (HasComp<StorageOrganComponent>(partUid) &&
                TryComp<StorageComponent>(partUid, out var storage) &&
                CanShowImplantedStorage(partUid, storage, bodyPart: true))
                yield return partUid;
        }
    }

    [SubscribeLocalEvent]
    private void OnBodyPartStorageRangeCheck(Entity<BodyPartComponent> part, ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (args.UiKey is not StorageComponent.StorageUiKey.Key ||
            !TryComp<StorageComponent>(part, out var storage) ||
            !CanShowImplantedStorage(part.Owner, storage, bodyPart: true) ||
            part.Comp.Body is not { } owner ||
            !HasComp<BodyComponent>(owner))
            return;

        SetStorageRangeResult(owner, ref args);
    }

    [SubscribeLocalEvent]
    private void OnCustomLimbStorageRangeCheck(Entity<CustomLimbMarkerComponent> limb,
        ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (args.UiKey is not StorageComponent.StorageUiKey.Key ||
            !TryComp<StorageComponent>(limb, out var storage) ||
            !CanShowImplantedStorage(limb.Owner, storage, bodyPart: false) ||
            limb.Comp.VirtualPart is not { } virtualPart ||
            !_containers.TryGetContainingContainer(limb.Owner, out var container) ||
            !TryComp<BodyComponent>(container.Owner, out var body))
            return;

        // The item is held in a hand slot on the wearer, which normal UI access treats as inaccessible
        // to another player. Only override that check while it is still an attached custom limb.
        var attached = false;
        foreach (var (partUid, _) in _body.GetBodyChildren(container.Owner, body))
        {
            if (partUid != virtualPart ||
                !TryComp<CustomLimbComponent>(partUid, out var customLimb) ||
                customLimb.Item != limb.Owner)
                continue;

            attached = true;
            break;
        }

        if (!attached)
            return;

        SetStorageRangeResult(container.Owner, ref args);
    }

    private void SetStorageRangeResult(EntityUid owner, ref BoundUserInterfaceCheckRangeEvent args) =>
        args.Result = args.Actor.Owner == owner ||
            _interaction.InRangeUnobstructed(args.Actor.Owner, owner, args.Data.InteractionRange)
                ? BoundUserInterfaceRangeResult.Pass
                : BoundUserInterfaceRangeResult.Fail;

    // Storage organs may hide the ordinary storage verb in favor of a limb action but remain visible in the strip view.
    private bool CanShowImplantedStorage(EntityUid uid, StorageComponent storage, bool bodyPart) =>
        IsPublicStorage(uid) &&
        (storage.ShowVerb ||
            HasComp<PrivateStorageComponent>(uid) ||
            (bodyPart && HasComp<StorageOrganComponent>(uid)));

    public bool IsPublicStorage(EntityUid storage) =>
        !HasComp<SubdermalImplantComponent>(storage) &&
        !HasComp<StorageImplantComponent>(storage);
}
