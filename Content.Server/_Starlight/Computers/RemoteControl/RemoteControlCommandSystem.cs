using Content.Server.Movement.Systems;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Hands;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.CombatMode;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.Computers.RemoteControl;

/// <summary>Executes authorized body commands for console and native-HUD controllers.</summary>
public sealed partial class RemoteControlCommandSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private PullController _pullController = default!;

    /// <summary>Executes an action after the caller has authorized control of the body.</summary>
    public bool ExecuteAction(EntityUid remoteEntity, EntityUid action, EntityUid? target = null, NetCoordinates? coordinates = null)
    {
        if (TerminatingOrDeleted(remoteEntity) || !_actionBlocker.CanConsciouslyPerformAction(remoteEntity)
            || !GetActionEntities(remoteEntity).Contains(action))
            return false;
        if (target != null || coordinates != null)
            return HasComp<TargetActionComponent>(action) && _actions.TryPerformAction(remoteEntity, action, target, coordinates);
        return _actions.TryPerformAction(remoteEntity, action);
    }

    /// <summary>Executes a world command after the caller has authorized control and the optional hand.</summary>
    public void ExecuteInteraction(EntityUid remoteEntity, EntityCoordinates coordinates,
        RemoteControlInteractionAction action = RemoteControlInteractionAction.Interact, EntityUid? target = null,
        bool altInteract = false, bool activateInWorld = false, AtmosPipeLayer? pipeLayer = null, string? hand = null)
    {
        if (TerminatingOrDeleted(remoteEntity) || !_actionBlocker.CanConsciouslyPerformAction(remoteEntity))
            return;

        if (action == RemoteControlInteractionAction.Drop)
        {
            _hands.TryDrop(remoteEntity, coordinates);
            return;
        }

        if (action == RemoteControlInteractionAction.Throw)
        {
            EntityManager.System<PredictedHandsSystem>()
                .ThrowHeldItem(remoteEntity, coordinates);
            return;
        }

        if (action == RemoteControlInteractionAction.Shoot)
        {
            if (TryComp<CombatModeComponent>(remoteEntity, out var shootCombatMode)
                && shootCombatMode.IsInCombatMode
                && EntityManager.System<SharedGunSystem>().TryGetGun(remoteEntity, out var gun)
                && gun.Comp.UseKey != altInteract)
                EntityManager.System<SharedGunSystem>().AttemptShoot(remoteEntity, gun,
                    coordinates, target);

            return;
        }

        if (action == RemoteControlInteractionAction.TryPull
            && target is { } pullTarget)
        {
            _interaction.TryPullObject(remoteEntity, pullTarget);
            return;
        }

        if (action == RemoteControlInteractionAction.MovePulledObject)
        {
            _pullController.MovePulledObject(
                new Entity<PullerComponent?>(remoteEntity, null), coordinates);
            return;
        }

        if (activateInWorld && target is { } activateTarget)
        {
            _interaction.InteractionActivate(remoteEntity, activateTarget, complexInteractions: true);
            return;
        }

        var activeItem = TryComp<HandsComponent>(remoteEntity, out var remoteHands)
            ? _hands.GetActiveItem((remoteEntity, remoteHands))
            : null;
        if (target == remoteEntity && activeItem is { } heldItem)
        {
            if (altInteract)
                _hands.TryUseItemInHand(remoteEntity, true);
            else
                _interaction.UseInHandInteraction(remoteEntity, heldItem);

            return;
        }

        if (target is { } item
            && TryComp<ItemComponent>(item, out var itemComp)
            && itemComp.AllowDirectHandPickup
            && !HasComp<ActivatableUIComponent>(item)
            && TryComp<HandsComponent>(remoteEntity, out var hands)
            && !_hands.TryGetActiveItem((remoteEntity, hands), out _)
            && _interaction.InRangeAndAccessible(remoteEntity, item))
        {
            if (hand is { } selectedHand)
            {
                _hands.TryPickup(remoteEntity, item, selectedHand, handsComp: hands, item: itemComp);
                return;
            }
            if (_hands.TryPickupAnyHand(remoteEntity, item, handsComp: hands, item: itemComp))
                return;
        }

        if (!altInteract
            && target is { } attackTarget
            && TryComp<CombatModeComponent>(remoteEntity, out var combatMode)
            && combatMode.IsInCombatMode
            && !HasComp<MapGridComponent>(attackTarget)
            && !HasComp<MapComponent>(attackTarget)
            && EntityManager.System<SharedMeleeWeaponSystem>()
                .TryGetWeapon(remoteEntity, out var weaponUid, out var weapon))
        {
            EntityManager.System<SharedMeleeWeaponSystem>()
                .AttemptLightAttack(remoteEntity, weaponUid, weapon, attackTarget);
            return;
        }

        if (pipeLayer is { } requestedLayer
            && activeItem is { } rcdItem
            && TryComp<RCDComponent>(rcdItem, out var rcd)
            && (rcd.IsRpd || rcd.IsRPLD)
            && rcd.CurrentMode == RpdMode.Free)
        {
            var selectedLayer = rcd.IsRPLD && requestedLayer > AtmosPipeLayer.Tertiary
                ? AtmosPipeLayer.Tertiary
                : requestedLayer;
            EntityManager.System<RCDSystem>().SetSelectedLayer((rcdItem, rcd), selectedLayer);
        }

        _interaction.UserInteraction(remoteEntity, coordinates, target, altInteract);
    }

    /// <summary>Executes a hand operation after the caller has authorized that hand.</summary>
    public void ExecuteHand(EntityUid remoteEntity, string hand, RemoteControlHandAction action)
    {
        if (TerminatingOrDeleted(remoteEntity) || !_actionBlocker.CanConsciouslyPerformAction(remoteEntity))
            return;

        switch (action)
        {
            case RemoteControlHandAction.SetActive:
                _hands.TrySetActiveHand(new Entity<HandsComponent?>(remoteEntity, null), hand);
                break;
            case RemoteControlHandAction.Use:
                _hands.TryUseItemInHand(remoteEntity, handName: hand);
                break;
            case RemoteControlHandAction.Activate:
                _hands.TryActivateItemInHand(remoteEntity, handName: hand);
                break;
            case RemoteControlHandAction.AltUse:
                _hands.TryUseItemInHand(remoteEntity, true, handName: hand);
                break;
            case RemoteControlHandAction.MoveToActive:
                _hands.TryMoveHeldEntityToActiveHand(remoteEntity, hand);
                break;
            case RemoteControlHandAction.Drop:
                _hands.TryDrop(remoteEntity, hand);
                break;
            case RemoteControlHandAction.CycleActive:
                _hands.TryCycleActiveHand(remoteEntity);
                break;
            case RemoteControlHandAction.CycleActiveReverse:
                _hands.TryCycleActiveHandReverse(remoteEntity);
                break;
            case RemoteControlHandAction.InteractWithHand:
                _hands.TryInteractHandWithActiveHand(remoteEntity, hand);
                break;
        }
    }

    /// <summary>Executes an inventory operation after the caller has authorized full inventory access.</summary>
    public void ExecuteInventory(EntityUid remoteEntity, string slot, RemoteControlInventoryAction action)
    {
        if (TerminatingOrDeleted(remoteEntity) || !_actionBlocker.CanConsciouslyPerformAction(remoteEntity) || !TryComp<InventoryComponent>(remoteEntity, out var inventory))
            return;

        switch (action)
        {
            case RemoteControlInventoryAction.UseSlot:
                UseRemoteSlot(remoteEntity, slot, inventory);
                break;
            case RemoteControlInventoryAction.OpenStorage:
                if (_inventory.TryGetSlotEntity(remoteEntity, slot, out var item, inventory)
                    && item is { } storageItem
                    && TryComp<StorageComponent>(storageItem, out var storage))
                {
                    _storage.OpenStorageUI(storageItem, remoteEntity, storage, false);
                }

                break;
        }
    }

    private void UseRemoteSlot(EntityUid remoteEntity, string slot, InventoryComponent inventory)
    {
        if (!TryComp<HandsComponent>(remoteEntity, out var hands))
            return;

        var held = _hands.GetActiveItem((remoteEntity, hands));
        _inventory.TryGetSlotEntity(remoteEntity, slot, out var item, inventory);

        if (held is { } heldItem && item is { } slotItem)
        {
            _interaction.InteractUsing(remoteEntity, heldItem, slotItem, Transform(slotItem).Coordinates);
            return;
        }

        if (item is { } equipped)
        {
            if (!_inventory.TryUnequip(remoteEntity, slot, out var removed, predicted: true,
                    inventory: inventory, checkDoafter: true, triggerHandContact: true))
                return;

            _hands.PickupOrDrop(remoteEntity, removed.Value);
            return;
        }

        if (held is { } heldEntity)
            _inventory.TryEquip(remoteEntity, heldEntity, slot, predicted: true, inventory: inventory,
                checkDoafter: true, triggerHandContact: true);
    }

    public HashSet<EntityUid> GetActionEntities(EntityUid remoteEntity)
    {
        var result = new HashSet<EntityUid>();
        if (TryComp<ActionsComponent>(remoteEntity, out var actions))
            foreach (var action in _actions.GetActions(remoteEntity, actions))
                result.Add(action.Owner);

        if (TryComp<BorgSwitchableTypeComponent>(remoteEntity, out var switchable)
            && switchable.SelectTypeAction is { } selectTypeAction
            && Exists(selectTypeAction))
            result.Add(selectTypeAction);

        if (!TryComp<BorgChassisComponent>(remoteEntity, out var chassis))
            return result;

        foreach (var module in chassis.ModuleContainer.ContainedEntities)
            if (TryComp<SelectableBorgModuleComponent>(module, out var selectable)
                && selectable.ModuleSwapActionEntity is { } action
                && Exists(action))
                result.Add(action);

        return result;
    }

    /// <summary>Collects the items and actions needed to display a controlled body.</summary>
    public void CollectVisibleEntities(EntityUid body, HashSet<EntityUid> entities, bool includeInventory)
    {
        if (TryComp<BorgChassisComponent>(body, out var chassis))
            foreach (var module in chassis.ModuleContainer.ContainedEntities)
                entities.Add(module);
        if (TryComp<HandsComponent>(body, out var hands))
            foreach (var hand in hands.SortedHands)
                if (_hands.TryGetHeldItem((body, hands), hand, out var item))
                    entities.Add(item.Value);
        entities.UnionWith(GetActionEntities(body));
        if (includeInventory && TryComp<InventoryComponent>(body, out var inventory))
            foreach (var slot in inventory.Slots)
                if (_inventory.TryGetSlotEntity(body, slot.Name, out var item, inventory))
                    entities.Add(item.Value);
    }
}
