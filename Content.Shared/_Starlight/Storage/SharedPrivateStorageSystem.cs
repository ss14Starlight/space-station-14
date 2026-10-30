using Content.Shared.DoAfter;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Strip;
using Content.Shared.Strip.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Storage;

/// <summary>
/// Modifies access to internal storage depending on whether the user initiating it is the owner of the storage.
/// </summary>
public abstract partial class SharedPrivateStorageSystem : EntitySystem
{
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedImplantedStorageAccessSystem _implantedStorage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StorageComponent, PrivateStorageDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<PrivateStorageComponent, GetVerbsEvent<ActivationVerb>>(AddPrivateStorageVerb,
            after: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<PrivateStorageComponent, ActivateInWorldEvent>(OnActivate, after: [typeof(SharedStrippableSystem)]);
        SubscribeLocalEvent<StorageComponent, BoundUserInterfaceCheckRangeEvent>(OnStorageRangeCheck);
        SubscribeLocalEvent<StrippableComponent, StrippingInternalStorageButtonPressed>(OnStrippingInternalStorageButtonPressed);
    }

    public void ToggleStorage(Entity<StorageComponent> storage, EntityUid owner, EntityUid user, bool fromStrippingView)
    {
        var uid = storage.Owner;
        if (fromStrippingView &&
            (!TryGetStrippableOwner(uid, user, out var currentOwner) ||
                currentOwner != owner ||
                !_implantedStorage.IsPublicStorage(uid)))
            return;

        if (!fromStrippingView && owner != uid)
            return;

        if (!CanInteract(user, uid))
            return;

        if (_ui.IsUiOpen(uid, StorageComponent.StorageUiKey.Key, user))
        {
            _ui.CloseUi(uid, StorageComponent.StorageUiKey.Key, user);
            return;
        }

        if (user != owner)
        {
            if (TryComp<PrivateStorageComponent>(uid, out var privateStorage))
            {
                StartStorageAccess(uid, owner, user, privateStorage.AccessDelay, privateStorage.AccessPopup,
                    fromStrippingView);
                return;
            }

            if (fromStrippingView)
            {
                StartStorageAccess(uid, owner, user,
                    PrivateStorageComponent.DefaultAccessDelay, PrivateStorageComponent.DefaultAccessPopup,
                    fromStrippingView);
                return;
            }
        }

        _storage.OpenStorageUI(uid, user, storage.Comp, false);
    }

    private void OnDoAfter(EntityUid uid, StorageComponent component, DoAfterEvent args)
    {
        if(args.Cancelled)
            return;

        if(args.Handled)
            return;

        if (!Exists(args.Args.User))
            return;

        if (args is PrivateStorageDoAfterEvent { FromStrippingView: true } &&
            (!TryGetStrippableOwner(uid, args.Args.User, out var currentOwner) ||
            args.Args.Target != currentOwner ||
            !_implantedStorage.IsPublicStorage(uid)))
            return;

        if (TryComp<StorageComponent>(uid, out var storageComp))
        {
            _storage.OpenStorageUI(uid, args.Args.User, storageComp, false);
        }

        args.Handled = true;
    }

    private void AddPrivateStorageVerb(EntityUid uid, PrivateStorageComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        if (IsCarrierStorage(uid))
            return;

        if (!TryComp<StorageComponent>(uid, out var storageComp))
            return;

        var accessOwner = uid;
        var inStrippingView = TryGetStrippableOwner(uid, args.User, out var strippableOwner);
        if (inStrippingView)
        {
            RemoveDefaultStorageVerb(args.Verbs);
            if (!_implantedStorage.IsPublicStorage(uid))
                return;

            accessOwner = strippableOwner;
        }

        if ((!args.CanAccess && !inStrippingView) ||
            !CanInteract(args.User, uid, args.CanInteract))
            return;

        AddStorageVerb((uid, storageComp), accessOwner, args.User, args.Verbs, inStrippingView);
    }

    public bool TryAddStrippableStorageVerb(EntityUid uid, StorageComponent storage, GetVerbsEvent<ActivationVerb> args)
    {
        if (HasComp<PrivateStorageComponent>(uid))
        {
            if (!IsCarrierStorage(uid) && !TryGetStrippableOwner(uid, args.User, out _))
                return false;

            RemoveDefaultStorageVerb(args.Verbs);
            return true;
        }

        if (!TryGetStrippableOwner(uid, args.User, out var owner))
            return false;

        RemoveDefaultStorageVerb(args.Verbs);
        if (!args.CanInteract ||
            !_implantedStorage.IsPublicStorage(uid) ||
            (!storage.ShowVerb && !HasComp<StorageOrganComponent>(uid)) ||
            !CanInteract(args.User, uid, args.CanInteract))
            return true;

        AddStorageVerb((uid, storage), owner, args.User, args.Verbs, fromStrippingView: true);
        return true;
    }

    private void RemoveDefaultStorageVerb(SortedSet<ActivationVerb> verbs)
    {
        var openText = Loc.GetString("comp-storage-verb-open-storage");
        var closeText = Loc.GetString("comp-storage-verb-close-storage");
        var defaults = new List<ActivationVerb>();

        foreach (var verb in verbs)
        {
            if (verb.Text == openText || verb.Text == closeText)
                defaults.Add(verb);
        }

        foreach (var verb in defaults)
        {
            verbs.Remove(verb);
        }
    }

    private void AddStorageVerb(
        Entity<StorageComponent> storage,
        EntityUid owner,
        EntityUid user,
        SortedSet<ActivationVerb> verbs,
        bool fromStrippingView)
    {
        var uiOpen = _ui.IsUiOpen(storage.Owner, StorageComponent.StorageUiKey.Key, user);
        var verb = new ActivationVerb
        {
            Act = () => ToggleStorage(storage, owner, user, fromStrippingView),
            Text = Loc.GetString(uiOpen ? "comp-storage-verb-close-storage" : "comp-storage-verb-open-storage"),
            Icon = new SpriteSpecifier.Texture(new ResPath(
                uiOpen
                    ? "/Textures/Interface/VerbIcons/close.svg.192dpi.png"
                    : "/Textures/Interface/VerbIcons/open.svg.192dpi.png")),
        };

        verbs.Add(verb);
    }

    private void OnStrippingInternalStorageButtonPressed(
        Entity<StrippableComponent> carrier,
        ref StrippingInternalStorageButtonPressed args)
    {
        var user = args.Actor;
        if (args.UiKey is not StrippingUiKey.Key ||
            !user.Valid ||
            !IsCarrierStorage(carrier.Owner) ||
            !HasComp<PrivateStorageComponent>(carrier.Owner) ||
            !TryGetStrippableOwner(carrier.Owner, user, out var owner) ||
            owner != carrier.Owner ||
            !TryComp<StorageComponent>(carrier.Owner, out var storage))
            return;

        ToggleStorage((carrier.Owner, storage), carrier.Owner, user, fromStrippingView: true);
    }

    private void OnStorageRangeCheck(Entity<StorageComponent> storage, ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (args.UiKey is not StorageComponent.StorageUiKey.Key ||
            !_implantedStorage.IsPublicStorage(storage.Owner) ||
            !TryGetStrippableOwner(storage.Owner, args.Actor.Owner, args.Data.InteractionRange, out _))
            return;

        args.Result = BoundUserInterfaceRangeResult.Pass;
    }

    /// <summary>
    /// Code used to open storage with action button over the verb
    /// Required to be separated from Storage System due to doAfter needed for others to open it
    /// </summary>
    private void OnActivate(EntityUid uid, PrivateStorageComponent storageComp, ActivateInWorldEvent args)
    {
        if ((IsCarrierStorage(uid) && args.User != uid) || args.Handled || !args.Complex || !CanInteract(args.User, uid))
            return;

        // Toggle
        if (_ui.IsUiOpen(uid, StorageComponent.StorageUiKey.Key, args.User))
        {
            _ui.CloseUi(uid, StorageComponent.StorageUiKey.Key, args.User);
        }
        else
        {
            StartStorageAccess(uid, uid, args.User, storageComp.AccessDelay, storageComp.AccessPopup,
                fromStrippingView: false);
        }

        args.Handled = true;
    }

    private bool CanInteract(EntityUid user, EntityUid storage, bool canInteract = true, bool silent = true)
    {
        if (HasComp<BypassInteractionChecksComponent>(user))
            return true;

        if (!canInteract)
            return false;

        var ev = new StorageInteractAttemptEvent(silent);
        RaiseLocalEvent(storage, ref ev);

        return !ev.Cancelled;
    }

    private bool TryGetStrippableOwner(EntityUid storage, EntityUid user, out EntityUid owner) =>
        TryGetStrippableOwner(storage, user, SharedInteractionSystem.InteractionRange, out owner);

    private bool TryGetStrippableOwner(EntityUid storage, EntityUid user, float interactionRange, out EntityUid owner)
    {
        owner = default;

        if (IsCarrierStorage(storage))
        {
            owner = storage;
        }
        else if (TryComp<BodyPartComponent>(storage, out var bodyPart) && bodyPart.Body is { } body)
        {
            owner = body;
        }
        else if (_containers.TryGetContainingContainer(storage, out var container))
        {
            owner = container.Owner;
        }
        else
        {
            return false;
        }

        if (!HasComp<StrippableComponent>(owner) ||
            !_ui.IsUiOpen(owner, StrippingUiKey.Key, user) ||
            (owner != user && !_interaction.InRangeUnobstructed(user, owner, interactionRange)))
        {
            owner = default;
            return false;
        }

        return true;
    }

    private bool IsCarrierStorage(EntityUid uid) =>
        HasComp<BodyComponent>(uid) &&
        HasComp<StrippableComponent>(uid) &&
        HasComp<StorageComponent>(uid);

    /// <summary>
    /// Starts outsider storage access with a delay and popup.
    /// If the user is the owner of the storage, access is granted immediately without delay.
    /// </summary>
    private void StartStorageAccess(EntityUid uid, EntityUid owner, EntityUid user,
        TimeSpan accessDelay, LocId accessPopup, bool fromStrippingView)
    {
        // If the user is the owner of the storage, grant immediate access
        if (owner == user)
        {
            if (TryComp<StorageComponent>(uid, out var storageComp))
            {
                _storage.OpenStorageUI(uid, user, storageComp, false);
            }

            return;
        }
        var beforeStripEvent = new BeforeStripEvent(accessDelay);
        RaiseLocalEvent(user, ref beforeStripEvent);
        var doAfterDelay = fromStrippingView ? beforeStripEvent.Time : accessDelay;

        // For other users, require DoAfter with delay
        var doAfterArgs = new DoAfterArgs(EntityManager, user, doAfterDelay,
            new PrivateStorageDoAfterEvent(fromStrippingView), uid, target: owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            Hidden = beforeStripEvent.Stealth
        };

        if (!beforeStripEvent.Stealth)
            _popup.PopupEntity(Loc.GetString(accessPopup, ("user", user)), owner, owner, PopupType.Medium);

        _doAfter.TryStartDoAfter(doAfterArgs);
    }
}
