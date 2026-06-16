using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Clothing.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Storage;

public sealed partial class RodentiaBagJumpSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<StorageComponent, RodentiaBagJumpDoAfterEvent>(OnDoAfter);
    }

    private void OnGetAlternativeVerbs(GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.User == args.Target)
            return;

        if (!TryComp<RodentiaBagJumpComponent>(args.User, out var component) || !_actionBlocker.CanMove(args.User))
            return;

        if (!TryGetTargetBag(args.Target, out var bag, out var attached) || !CanJumpInto(args.User, bag))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("rodentia-bag-jump-verb"),
            Act = () => StartJump(args.User, bag, args.Target, attached, component),
            Priority = 2,
            Impact = LogImpact.Medium,
        });
    }

    private void OnDoAfter(Entity<StorageComponent> ent, ref RodentiaBagJumpDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (!CanJumpInto(args.User, ent.Owner) || _container.IsEntityInContainer(ent.Owner))
            return;

        if (args.Attached && (args.Target == null || args.Used != ent.Owner || !IsBackBag(args.Target.Value, ent.Owner)))
            return;

        if (!args.Attached && args.Target != ent.Owner)
            return;

        if (!_storage.Insert(ent.Owner, args.User, out _, args.User, ent.Comp))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):player} jumped into bag {ToPrettyString(ent.Owner)}");
    }

    private void StartJump(EntityUid user, EntityUid bag, EntityUid clicked, bool attached, RodentiaBagJumpComponent component)
    {
        var delay = attached ? component.AttachedBagDelay : component.GroundBagDelay;
        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay, new RodentiaBagJumpDoAfterEvent(attached), bag, target: attached ? clicked : bag, used: attached ? bag : null)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private bool TryGetTargetBag(EntityUid target, out EntityUid bag, out bool attached)
    {
        bag = EntityUid.Invalid;
        attached = false;

        if (IsBag(target) && !_container.IsEntityInContainer(target))
        {
            bag = target;
            return true;
        }

        if (!_inventory.TryGetSlotEntity(target, "back", out var back) || !IsBag(back.Value))
            return false;

        bag = back.Value;
        attached = true;
        return true;
    }

    private bool IsBackBag(EntityUid wearer, EntityUid bag) =>
        _inventory.TryGetSlotEntity(wearer, "back", out var back) && back == bag && IsBag(bag);

    private bool IsBag(EntityUid uid) =>
        TryComp<StorageComponent>(uid, out _)
        && TryComp<ClothingComponent>(uid, out var clothing)
        && (clothing.Slots & SlotFlags.BACK) != 0;

    private bool CanJumpInto(EntityUid user, EntityUid bag) =>
        TryComp<StorageComponent>(bag, out var storage)
        && _storage.CanInsert(bag, user, out _, storage);

    [Serializable, NetSerializable]
    public sealed partial class RodentiaBagJumpDoAfterEvent : DoAfterEvent
    {
        public bool Attached;

        public RodentiaBagJumpDoAfterEvent(bool attached) => Attached = attached;

        public override DoAfterEvent Clone() => new RodentiaBagJumpDoAfterEvent(Attached);
    }
}
