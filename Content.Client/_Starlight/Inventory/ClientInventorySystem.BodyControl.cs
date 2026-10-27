using Content.Client._Starlight.BodyControl;
using Content.Shared._Starlight.BodyControl;

// ReSharper disable CheckNamespace
namespace Content.Client.Inventory;

public sealed partial class ClientInventorySystem
{
    [Dependency] private BodyControlViewSystem _bodyView = default!;

    private EntityUid? InventoryUiEntity => _bodyView.Entity;

    [SubscribeLocalEvent]
    private void OnControlViewChanged(BodyControlViewChangedEvent args)
    {
        OnUnlinkInventory?.Invoke();
        ReloadInventory();
    }

    private bool TryControlInventoryUse(string slot)
        => _bodyView.Send(new BodyControlCommandEvent(BodyControlCommand.InventoryUse, slot: slot));

    private bool TryControlInventoryStorage(string slot)
        => _bodyView.Send(new BodyControlCommandEvent(BodyControlCommand.InventoryStorage, slot: slot));

    private bool TryControlInventoryActivate(string slot)
        => _bodyView.Send(new BodyControlCommandEvent(BodyControlCommand.InventoryActivate, slot: slot));

    private bool TryControlInventoryAltActivate(string slot)
        => _bodyView.Send(new BodyControlCommandEvent(BodyControlCommand.InventoryAltActivate, slot: slot));
}
