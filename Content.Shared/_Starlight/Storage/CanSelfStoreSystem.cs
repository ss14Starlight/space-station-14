using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Storage;

public sealed partial class CanSelfStoreSystem : EntitySystem
{
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;

    [SubscribeLocalEvent]
    private void OnGetVerbs(EntityUid uid, StorageComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        if (user == uid ||
            !args.CanAccess ||
            !args.CanInteract ||
            !CanSelfStore(user, uid, component))
        {
            return;
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("storage-verb-store-self"),
            DoContactInteraction = false,
            Act = () => StartSelfStore(uid, user)
        });
    }
    [SubscribeLocalEvent]
    private void OnCanDropTarget(EntityUid uid, StorageComponent component, ref CanDropTargetEvent args)
    {
        if (args.Handled || args.User != args.Dragged || !CanSelfStore(args.User, uid, component))
            return;

        args.CanDrop = true;
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnDragDropTarget(EntityUid uid, StorageComponent component, ref DragDropTargetEvent args)
    {
        if (args.Handled || args.User != args.Dragged || !CanSelfStore(args.User, uid, component))
            return;

        StartSelfStore(uid, args.User);
        args.Handled = true;
    }

    private bool CanSelfStore(EntityUid user, EntityUid storage, StorageComponent component)
    {
        if (user == storage || !_actionBlocker.CanInteract(user, storage))
            return false;

        var ev = new StorageInteractAttemptEvent(Silent: true);
        RaiseLocalEvent(storage, ref ev);
        return !ev.Cancelled && _storage.CanInsert(storage, user, out _, component);
    }

    private void StartSelfStore(EntityUid storage, EntityUid user)
    {
        if (!TryComp<StorageComponent>(storage, out var component) ||
            !CanSelfStore(user, storage, component))
        {
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, user, 5f, new CanSelfStoreDoAfterEvent(), eventTarget: storage, target: storage)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            BlockDuplicate = true,
            CancelDuplicate = false,
            DuplicateCondition = DuplicateConditions.SameEvent
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    [SubscribeLocalEvent]
    private void OnStoreDoAfter(EntityUid uid, StorageComponent component, CanSelfStoreDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target != uid)
            return;

        if (!CanSelfStore(args.User, uid, component))
        {
            return;
        }

        if(_storage.PlayerInsertEntityInWorld((uid, component), args.User, args.User))
            args.Handled = true;
    }
}
