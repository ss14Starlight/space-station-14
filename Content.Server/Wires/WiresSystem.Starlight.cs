using Content.Shared.Hands.Components;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.Tools.Components;
using Content.Shared.Wires;

namespace Content.Server.Wires;

public sealed partial class WiresSystem
{
    [Dependency] private SharedRemoteControlConsoleSystem _sharedRemoteControl = default!;

    public bool TryOpenRemoteUserInterface(
        EntityUid uid,
        EntityUid remoteUser,
        EntityUid used,
        WiresComponent wires)
    {
        if (!_sharedRemoteControl.TryGetControllerForRemoteEntity(remoteUser, out _))
            return false;

        wires.ViewWires = _tags.HasTag(used, wires.ShowWiresTag);
        UpdateUserInterface(uid, wires);
        _uiSystem.OpenUi(uid, WiresUiKey.Key, remoteUser);
        return true;
    }

    public void TryDoRemoteWireAction(EntityUid uid, EntityUid remoteUser, WiresActionMessage message)
    {
        if (!TryComp(uid, out WiresComponent? wires))
            return;

        if (!TryComp(remoteUser, out HandsComponent? hands))
        {
            _popupSystem.PopupEntity(Loc.GetString("wires-component-ui-on-receive-message-no-hands"), uid, remoteUser);
            return;
        }

        if (!_interactionSystem.InRangeUnobstructed(remoteUser, uid))
        {
            _popupSystem.PopupEntity(Loc.GetString("wires-component-ui-on-receive-message-cannot-reach"), uid, remoteUser);
            return;
        }

        if (!_hands.TryGetActiveItem((remoteUser, hands), out var heldEntity)
            || !TryComp(heldEntity, out ToolComponent? tool))
            return;

        TryDoWireAction(uid, remoteUser, heldEntity.Value, message.Id, message.Action, wires, tool);
    }
}
