using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Storage;

/// <summary>
/// Adds access verbs to a body for storage that is visibly attached as a body part or custom item limb.
/// Secret subdermal storage is deliberately excluded.
/// </summary>
public sealed partial class SharedImplantedStorageAccessSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedPrivateStorageSystem _privateStorage = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BodyComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BodyPartComponent, BoundUserInterfaceCheckRangeEvent>(OnBodyPartStorageRangeCheck);
        SubscribeLocalEvent<CustomLimbMarkerComponent, BoundUserInterfaceCheckRangeEvent>(OnCustomLimbStorageRangeCheck);
    }

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

    private void OnCustomLimbStorageRangeCheck(Entity<CustomLimbMarkerComponent> limb, ref BoundUserInterfaceCheckRangeEvent args)
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

    private void SetStorageRangeResult(EntityUid owner, ref BoundUserInterfaceCheckRangeEvent args)
    {
        args.Result = args.Actor.Owner == owner ||
            _interaction.InRangeUnobstructed(args.Actor.Owner, owner, args.Data.InteractionRange)
                ? BoundUserInterfaceRangeResult.Pass
                : BoundUserInterfaceRangeResult.Fail;
    }

    private void OnGetVerbs(Entity<BodyComponent> body, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var owner = body.Owner;
        var user = args.User;
        var storages = new HashSet<EntityUid>();
        foreach (var (partUid, _) in _body.GetBodyChildren(body.Owner, body.Comp))
        {
            if (TryComp<StorageComponent>(partUid, out var partStorage) &&
                CanShowImplantedStorage(partUid, partStorage, bodyPart: true))
                storages.Add(partUid);

            if (TryComp<CustomLimbComponent>(partUid, out var customLimb) &&
                TryComp<StorageComponent>(customLimb.Item, out var itemStorage) &&
                CanShowImplantedStorage(customLimb.Item, itemStorage, bodyPart: false))
            {
                storages.Add(customLimb.Item);
            }
        }

        foreach (var storageUid in storages)
        {
            if (!TryComp<StorageComponent>(storageUid, out var storage))
                continue;

            args.Verbs.Add(new ActivationVerb
            {
                Text = Loc.GetString("implanted-storage-verb-open"),
                Message = Loc.GetString("implanted-storage-verb-tooltip", ("storage", Name(storageUid))),
                IconEntity = GetNetEntity(storageUid),
                Act = () => _privateStorage.ToggleStorage((storageUid, storage), owner, user),
                Priority = 1,
            });
        }
    }

    private bool CanShowImplantedStorage(EntityUid uid, StorageComponent storage, bool bodyPart)
    {
        // Cargo legs hide the ordinary storage verb in favor of their limb action, but still need body-context access.
        return IsPublicStorage(uid) &&
            (storage.ShowVerb || (bodyPart && HasComp<StorageOrganComponent>(uid)));
    }

    private bool IsPublicStorage(EntityUid storage)
    {
        return !HasComp<SubdermalImplantComponent>(storage) &&
            !HasComp<StorageImplantComponent>(storage);
    }

}
