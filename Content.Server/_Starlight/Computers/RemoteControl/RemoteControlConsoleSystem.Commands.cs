using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Hands;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.CombatMode;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Content.Shared.Storage;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleSystem
{
    /// <summary>Executes an action after the caller has authorized control of the body.</summary>
    public bool ExecuteRemoteAction(EntityUid remoteEntity, EntityUid action, EntityUid? target = null, NetCoordinates? coordinates = null)
    {
        if (TerminatingOrDeleted(remoteEntity) || !CanRemoteControlledEntityAct(remoteEntity)
            || !GetRemoteActionEntities(remoteEntity).Contains(action))
            return false;
        if (coordinates is { } position)
            return HasComp<TargetActionComponent>(action) && _actions.TryPerformAction(remoteEntity, action, target, position);
        return _actions.TryPerformAction(remoteEntity, action);
    }

    /// <summary>Executes a world command after the caller has authorized control and the optional hand.</summary>
    public void ExecuteRemoteInteraction(EntityUid remoteEntity, RemoteControlInteractionMessage args, string? hand = null)
    {
        if (TerminatingOrDeleted(remoteEntity) || !CanRemoteControlledEntityAct(remoteEntity))
            return;

        var target = args.Target is { } targetNet ? GetEntity(targetNet) : (EntityUid?)null;

        if (args.Action == RemoteControlInteractionAction.Drop)
        {
            _hands.TryDrop(remoteEntity, GetCoordinates(args.Coordinates));
            return;
        }

        if (args.Action == RemoteControlInteractionAction.Throw)
        {
            EntityManager.System<PredictedHandsSystem>()
                .ThrowHeldItem(remoteEntity, GetCoordinates(args.Coordinates));
            return;
        }

        if (args.Action == RemoteControlInteractionAction.Shoot)
        {
            if (TryComp<CombatModeComponent>(remoteEntity, out var shootCombatMode)
                && shootCombatMode.IsInCombatMode
                && EntityManager.System<SharedGunSystem>().TryGetGun(remoteEntity, out var gun)
                && gun.Comp.UseKey != args.AltInteract)
                EntityManager.System<SharedGunSystem>().AttemptShoot(remoteEntity, gun,
                    GetCoordinates(args.Coordinates), target);

            return;
        }

        if (args.Action == RemoteControlInteractionAction.TryPull
            && target is { } pullTarget)
        {
            _interaction.TryPullObject(remoteEntity, pullTarget);
            return;
        }

        if (args.Action == RemoteControlInteractionAction.MovePulledObject)
        {
            _pullController.MovePulledObject(
                new Entity<PullerComponent?>(remoteEntity, null), GetCoordinates(args.Coordinates));
            return;
        }

        if (args.ActivateInWorld && target is { } activateTarget)
        {
            _interaction.InteractionActivate(remoteEntity, activateTarget, complexInteractions: true);
            return;
        }

        var activeItem = TryComp<HandsComponent>(remoteEntity, out var remoteHands)
            ? _hands.GetActiveItem((remoteEntity, remoteHands))
            : null;
        if (target == remoteEntity && activeItem is { } heldItem)
        {
            if (args.AltInteract)
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

        if (!args.AltInteract
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

        if (args.PipeLayer is { } pipeLayer
            && activeItem is { } rcdItem
            && TryComp<RCDComponent>(rcdItem, out var rcd)
            && (rcd.IsRpd || rcd.IsRPLD)
            && rcd.CurrentMode == RpdMode.Free)
        {
            var selectedLayer = rcd.IsRPLD && pipeLayer > AtmosPipeLayer.Tertiary
                ? AtmosPipeLayer.Tertiary
                : pipeLayer;
            EntityManager.System<RCDSystem>().SetSelectedLayer((rcdItem, rcd), selectedLayer);
        }

        _interaction.UserInteraction(remoteEntity, GetCoordinates(args.Coordinates), target, args.AltInteract);
    }

    /// <summary>Executes a hand operation after the caller has authorized that hand.</summary>
    public void ExecuteRemoteHand(EntityUid remoteEntity, RemoteControlHandMessage args)
    {
        if (TerminatingOrDeleted(remoteEntity) || !CanRemoteControlledEntityAct(remoteEntity))
            return;

        switch (args.Action)
        {
            case RemoteControlHandAction.SetActive:
                _hands.TrySetActiveHand(new Entity<HandsComponent?>(remoteEntity, null), args.Hand);
                break;
            case RemoteControlHandAction.Use:
                _hands.TryUseItemInHand(remoteEntity, handName: args.Hand);
                break;
            case RemoteControlHandAction.Activate:
                _hands.TryActivateItemInHand(remoteEntity, handName: args.Hand);
                break;
            case RemoteControlHandAction.AltUse:
                _hands.TryUseItemInHand(remoteEntity, true, handName: args.Hand);
                break;
            case RemoteControlHandAction.MoveToActive:
                _hands.TryMoveHeldEntityToActiveHand(remoteEntity, args.Hand);
                break;
            case RemoteControlHandAction.Drop:
                _hands.TryDrop(remoteEntity, args.Hand);
                break;
            case RemoteControlHandAction.CycleActive:
                _hands.TryCycleActiveHand(remoteEntity);
                break;
            case RemoteControlHandAction.CycleActiveReverse:
                _hands.TryCycleActiveHandReverse(remoteEntity);
                break;
            case RemoteControlHandAction.InteractWithHand:
                _hands.TryInteractHandWithActiveHand(remoteEntity, args.Hand);
                break;
        }
    }

    /// <summary>Executes an inventory operation after the caller has authorized full inventory access.</summary>
    public void ExecuteRemoteInventory(EntityUid remoteEntity, RemoteControlInventoryMessage args)
    {
        if (TerminatingOrDeleted(remoteEntity) || !CanRemoteControlledEntityAct(remoteEntity) || !TryComp<InventoryComponent>(remoteEntity, out var inventory))
            return;

        switch (args.Action)
        {
            case RemoteControlInventoryAction.UseSlot:
                UseRemoteSlot(remoteEntity, args.Slot, inventory);
                break;
            case RemoteControlInventoryAction.OpenStorage:
                if (_inventory.TryGetSlotEntity(remoteEntity, args.Slot, out var item, inventory)
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
}
