using Content.Shared.Actions;
using Content.Shared.Cuffs;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared._Starlight.Cybernetics; // Starlight
using Content.Shared._Starlight.Cybernetics.Components; // Starlight
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.RetractableItemAction;

/// <summary>
/// System for handling retractable items, such as armblades.
/// </summary>
public sealed partial class RetractableItemActionSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!; // 🌟Starlight🌟
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popups = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RetractableItemActionComponent, MapInitEvent>(OnActionInit);
        SubscribeLocalEvent<RetractableItemActionComponent, OnRetractableItemActionEvent>(OnRetractableItemAction);

        SubscribeLocalEvent<ActionRetractableItemComponent, ComponentShutdown>(OnActionSummonedShutdown);
        Subs.SubscribeWithRelay<ActionRetractableItemComponent, HeldRelayedEvent<TargetHandcuffedEvent>>(OnItemHandcuffed, inventory: false);

        SubscribeLocalEvent<RetractableItemActionComponent, CyberneticDisruptionEvent>(OnCyberneticsDisrupted); // 🌟Starlight🌟
    }

    private void OnActionInit(Entity<RetractableItemActionComponent> ent, ref MapInitEvent args)
    {
        _containers.EnsureContainer<Container>(ent, RetractableItemActionComponent.ContainerId);

        PopulateActionItem(ent.Owner);
    }

    private void OnRetractableItemAction(Entity<RetractableItemActionComponent> ent, ref OnRetractableItemActionEvent args)
    {
        if (args.Handled)
            return;

        /*  🌟Starlight🌟 Start
         *  if (_hands.GetActiveHand(args.Performer) is not { } activeHand) // Moved
         *      return;
         *  🌟Starlight🌟 End */

        if (_actions.GetAction(ent.Owner) is not { } action)
            return;

        if (action.Comp.AttachedEntity == null)
            return;

        if (ent.Comp.ActionItemUid == null)
            return;

        // 🌟Starlight🌟 start
        // A lot of this is the same, but moved a lot
        if (ent.Comp.SpawnInHand)
        {
            if (_hands.GetActiveHand(args.Performer) is not { } activeHand)
                return;

            // Don't allow to summon an item if holding an unremoveable item unless that item is summoned by the action.
            if (_hands.GetActiveItem(args.Performer) != null
                && !_hands.IsHolding(args.Performer, ent.Comp.ActionItemUid)
                && !_hands.CanDropHeld(args.Performer, activeHand, false))
            {
                _popups.PopupClient(Loc.GetString("retractable-item-hand-cannot-drop"), args.Performer, args.Performer);
                return;
            }

            if (_hands.IsHolding(args.Performer, ent.Comp.ActionItemUid))
            {
                args.Handled = RetractRetractableItem(args.Performer, ent.Comp.ActionItemUid.Value, ent.Owner);
            }
            else
            {
                // Don't allow summoning an item if it's from a cybernetic and the user is currently disrupted.
                if (ent.Comp.IsCybernetic && TryComp(args.Performer, out CyberneticDisruptionComponent? _))
                {
                    _popups.PopupClient(Loc.GetString("retractable-item-cybernetics-disrupted"), args.Performer, args.Performer);
                    return;
                }

                args.Handled = SummonRetractableItem(args.Performer, ent.Comp.ActionItemUid.Value, activeHand, ent.Owner);
            }
        }
        else
        {
            if (_inventory.InSlotWithFlags(ent.Comp.ActionItemUid.Value, ent.Comp.RequiredSlots))
            {
                args.Handled = RetractRetractableItem(args.Performer, ent.Comp.ActionItemUid.Value, ent.Owner);
            }
            else
            {
                // Don't allow summoning an item if it's from a cybernetic and the user is currently disrupted.
                if (ent.Comp.IsCybernetic && TryComp(args.Performer, out CyberneticDisruptionComponent? _))
                {
                    _popups.PopupClient(Loc.GetString("retractable-item-cybernetics-disrupted"), args.Performer, args.Performer);
                    return;
                }
                args.Handled = SummonRetractableItemInInventory(args.Performer, ent.Comp.ActionItemUid.Value, ent.Comp.Slot, ent.Owner);
            }
        }
        // 🌟Starlight🌟 end
    }

    private void OnActionSummonedShutdown(Entity<ActionRetractableItemComponent> ent, ref ComponentShutdown args)
    {
        if (_actions.GetAction(ent.Comp.SummoningAction) is not { } action)
            return;

        if (!TryComp<RetractableItemActionComponent>(action, out var retract) || retract.ActionItemUid != ent.Owner)
            return;

        // If the item is somehow destroyed, re-add it to the action.
        PopulateActionItem(action.Owner);
    }

    private void OnItemHandcuffed(Entity<ActionRetractableItemComponent> ent, ref HeldRelayedEvent<TargetHandcuffedEvent> args)
    {
        if (_actions.GetAction(ent.Comp.SummoningAction) is not { } action)
            return;

        if (action.Comp.AttachedEntity == null)
            return;

        if (_hands.GetActiveHand(action.Comp.AttachedEntity.Value) is not { })
            return;

        RetractRetractableItem(action.Comp.AttachedEntity.Value, ent, action.Owner);
    }

    private void PopulateActionItem(Entity<RetractableItemActionComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false) || TerminatingOrDeleted(ent))
            return;

        if (!PredictedTrySpawnInContainer(ent.Comp.SpawnedPrototype, ent.Owner, RetractableItemActionComponent.ContainerId, out var summoned))
            return;

        ent.Comp.ActionItemUid = summoned.Value;

        // Mark the unremovable item so it can be added back into the action.
        var summonedComp = AddComp<ActionRetractableItemComponent>(summoned.Value);
        summonedComp.SummoningAction = ent.Owner;
        Dirty(summoned.Value, summonedComp);

        Dirty(ent);
    }

    public void SetPrototype(Entity<RetractableItemActionComponent> ent, EntProtoId prototype)
    {
        if (ent.Comp.SpawnedPrototype == prototype)
            return;
        ent.Comp.SpawnedPrototype = prototype;
        var holder = _actions.GetAction(ent.Owner)?.Comp.AttachedEntity;
        var old = ent.Comp.ActionItemUid;
        var held = holder != null && old != null && _hands.IsHolding(holder.Value, old.Value);
        ent.Comp.ActionItemUid = null;
        if (old is { } item && !TerminatingOrDeleted(item))
        {
            RemComp<ActionRetractableItemComponent>(item);
            if (_containers.TryGetContainingContainer((item, null, null), out var container))
                _containers.Remove(item, container, force: true);
            QueueDel(item);
        }
        PopulateActionItem(ent.Owner);
        if (held && ent.Comp.ActionItemUid is { } replacement && _hands.TryForcePickupAnyHand(holder!.Value, replacement))
            EnsureComp<UnremoveableComponent>(replacement);
        Dirty(ent);
    }

    public void Retract(Entity<RetractableItemActionComponent> ent, EntityUid holder)
    {
        if (ent.Comp.ActionItemUid is { } item && _hands.IsHolding(holder, item))
            RetractRetractableItem(holder, item, ent.Owner);
    }

    private bool RetractRetractableItem(EntityUid holder, EntityUid item, Entity<RetractableItemActionComponent?> action)
    {
        if (!Resolve(action, ref action.Comp, false))
            return false;

        var unremoveable = HasComp<UnremoveableComponent>(item);
        RemComp<UnremoveableComponent>(item);
        var container = _containers.GetContainer(action, RetractableItemActionComponent.ContainerId);
        if (!_containers.Insert(item, container))
        {
            if (unremoveable)
                EnsureComp<UnremoveableComponent>(item);
            return false;
        }
        _audio.PlayPredicted(action.Comp.RetractSounds, holder, holder);
        return true;
    }

    private bool SummonRetractableItem(EntityUid holder, EntityUid item, string hand, Entity<RetractableItemActionComponent?> action)
    {
        if (!Resolve(action, ref action.Comp, false) || !_hands.TryForcePickupAnyHand(holder, item))
            return false;

        _audio.PlayPredicted(action.Comp.SummonSounds, holder, holder);
        EnsureComp<UnremoveableComponent>(item);
        return true;
    }

    #region Starlight
    private bool SummonRetractableItemInInventory(EntityUid holder, EntityUid item, string slot, Entity<RetractableItemActionComponent?> action)
    {
        if (!Resolve(action, ref action.Comp, false))
            return false;

        if (!_inventory.TryEquip(holder, item, slot, silent: true, force: true))
            return false;
        _audio.PlayPredicted(action.Comp.SummonSounds, holder, holder);
        EnsureComp<UnremoveableComponent>(item);
        return true;
    }

    private void OnCyberneticsDisrupted(Entity<RetractableItemActionComponent> ent, ref CyberneticDisruptionEvent args)
    {
        if(!ent.Comp.IsCybernetic)
            return;

        var ev = new OnRetractableItemActionEvent
        {
            Performer = args.Target,
        };
        RaiseLocalEvent(ent, ref ev);
    }

    #endregion Starlight
}
