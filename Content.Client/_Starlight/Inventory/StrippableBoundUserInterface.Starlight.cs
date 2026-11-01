// ReSharper disable CheckNamespace

using Content.Client._Starlight.Replay;
using Content.Client.UserInterface.Controls;
using Content.Client.Verbs.UI;
using Content.Shared.Hands.Components;
using Content.Shared.Input;
using Content.Shared.Inventory;
using Content.Shared._Starlight.Storage;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client.Inventory;

public sealed partial class StrippableBoundUserInterface
{
    private static readonly EntProtoId InternalStoragePrototype = "MobAdultSlimesGreen";

    // In replays, left-click or E opens a bag instead of stripping, and right-click opens the item's verbs.
    private bool TryReplaySlotPressed(GUIBoundKeyEventArgs ev, SlotControl slot)
    {
        var replay = EntMan.System<ReplayObserverSystem>();
        if (!replay.IsViewing())
            return false;

        if (ev.Function == EngineKeyFunctions.Use || ev.Function == ContentKeyFunctions.ActivateItemInWorld)
        {
            if (slot.Entity is { } item)
                replay.TryViewContents(item);

            return true;
        }

        if (ev.Function == EngineKeyFunctions.UseSecondary && slot.Entity is { } target)
        {
            replay.OpenVerbMenu(target);
            return true;
        }

        return false;
    }

    private void AddFunctionalHandButtons(Entity<HandsComponent> ent)
    {
        foreach (var (id, hand) in ent.Comp.Hands)
        {
            if (hand.Location != HandLocation.Functional)
                continue;

            AddHandButton(ent, id, hand);
        }

        AddStrippableStorageHandButtons(ent.Owner);
    }

    private void AddStrippableStorageHandButtons(EntityUid owner)
    {
        var storageAccess = EntMan.System<SharedImplantedStorageAccessSystem>();
        foreach (var storage in storageAccess.GetStrippableStorageHandEntities(owner))
        {
            var button = new SlotButton
            {
                ButtonTexturePath = "Slots/hand_m",
                Name = $"StorageHand_{storage}",
            };
            button.Pressed += BodyPartSlotPressed;
            UpdateEntityIcon(button, storage);
            _strippingMenu!.HandsContainer.AddChild(button);
            LayoutContainer.SetPosition(button,
                new Vector2i(_handCount, 0) * (SlotControl.DefaultButtonSize + ButtonSeparation));
            _handCount++;
        }
    }

    private void AddStrippableStorageButtons(EntityUid owner)
    {
        var storageAccess = EntMan.System<SharedImplantedStorageAccessSystem>();
        var columnCount = Math.Max(4, _inventoryDimensions.X + 1);
        var firstRow = _inventoryDimensions.Y + 1;
        var index = 0;

        foreach (var storage in storageAccess.GetStrippableStorageEntities(owner))
        {
            var button = new SlotButton
            {
                ButtonTexturePath = "Slots/hand_m",
                Name = $"Storage_{storage}"
            };
            if (storage == owner)
            {
                button.Pressed += InternalStorageSlotPressed;
                button.SetEntity(storage);
                button.SetPrototype(InternalStoragePrototype, false);
            }
            else
            {
                button.Pressed += BodyPartSlotPressed;
                UpdateEntityIcon(button, storage);
            }

            _strippingMenu!.InventoryContainer.AddChild(button);

            var position = new Vector2i(index % columnCount, firstRow + (index / columnCount));
            if (storage == owner &&
                EntMan.TryGetComponent<InventoryComponent>(owner, out var inventory) &&
                _inv.TryGetSlotContainer(owner, "shoes", out _, out var shoesSlot, inventory))
            {
                position = shoesSlot.StrippingWindowPos + new Vector2i(1, 0);
            }
            else
            {
                index++;
            }

            LayoutContainer.SetPosition(button, position * (SlotControl.DefaultButtonSize + ButtonSeparation));
            if (position.X > _inventoryDimensions.X)
                _inventoryDimensions = new Vector2i(position.X, _inventoryDimensions.Y);
            if (position.Y > _inventoryDimensions.Y)
                _inventoryDimensions = new Vector2i(_inventoryDimensions.X, position.Y);
        }
    }

    private void InternalStorageSlotPressed(GUIBoundKeyEventArgs ev, SlotControl slot)
    {
        if (slot.Entity is not { } entity)
            return;

        if (ev.Function == ContentKeyFunctions.ExamineEntity)
        {
            _examine.DoExamine(entity);
            ev.Handle();
        }
        else if (ev.Function == EngineKeyFunctions.UseSecondary)
        {
            var replay = EntMan.System<ReplayObserverSystem>();
            if (replay.IsViewing())
                replay.TryViewContents(entity);
            else
                SendPredictedMessage(new StrippingInternalStorageButtonPressed());

            ev.Handle();
        }
    }

    private void BodyPartSlotPressed(GUIBoundKeyEventArgs ev, SlotControl slot)
    {
        if (TryReplaySlotPressed(ev, slot))
            return;

        if (slot.Entity is not { } entity)
            return;

        if (ev.Function == ContentKeyFunctions.ExamineEntity)
        {
            _examine.DoExamine(entity);
            ev.Handle();
        }
        else if (ev.Function == EngineKeyFunctions.UseSecondary)
        {
            _ui.GetUIController<VerbMenuUIController>().OpenVerbMenu(entity);
            ev.Handle();
        }
    }
}
