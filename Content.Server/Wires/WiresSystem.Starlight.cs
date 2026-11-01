using Content.Shared.Hands.Components;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.Tools.Components;
using Content.Shared.Wires;

namespace Content.Server.Wires;

public sealed partial class WiresSystem
{
    [Dependency] private SharedRemoteControlConsoleSystem _sharedRemoteControl = default!;

    #region Starlight
    /// <summary>
    /// Attempts to open the wires user interface for a remote user.
    /// </summary>
    /// <param name="target">The wired entity whose interface should be opened.</param>
    /// <param name="remoteUser">The user acting through the remote interface.</param>
    /// <param name="used">The item used to interact with the wires.</param>
    /// <returns>True if the interface was opened.</returns>
    public bool TryOpenRemoteUserInterface(
        Entity<WiresComponent?> target,
        EntityUid remoteUser,
        EntityUid used)
    {
        if (!Resolve(target.Owner, ref target.Comp))
            return false;

        if (!_sharedRemoteControl.TryGetControllerForRemoteEntity(remoteUser, out _))
            return false;

        target.Comp.ViewWires = _tags.HasTag(used, target.Comp.ShowWiresTag);
        UpdateUserInterface(target.Owner, target.Comp);
        _uiSystem.OpenUi(target.Owner, WiresUiKey.Key, remoteUser);
        return true;
    }

    /// <summary>
    /// Attempts to perform a wire action initiated by a remote user.
    /// </summary>
    /// <param name="target">The wired entity whose wires are being changed.</param>
    /// <param name="remoteUser">The user acting through the remote interface.</param>
    /// <param name="message">The requested wire action.</param>
    public void TryDoRemoteWireAction(
        Entity<WiresComponent?> target,
        EntityUid remoteUser,
        WiresActionMessage message)
    {
        if (!Resolve(target.Owner, ref target.Comp))
            return;

        if (!TryComp(remoteUser, out HandsComponent? hands))
        {
            _popupSystem.PopupEntity(Loc.GetString("wires-component-ui-on-receive-message-no-hands"), target.Owner, remoteUser);
            return;
        }

        if (!_interactionSystem.InRangeUnobstructed(remoteUser, target.Owner))
        {
            _popupSystem.PopupEntity(Loc.GetString("wires-component-ui-on-receive-message-cannot-reach"), target.Owner, remoteUser);
            return;
        }

        if (!_hands.TryGetActiveItem((remoteUser, hands), out var heldEntity)
            || !TryComp(heldEntity, out ToolComponent? tool))
            return;

        TryDoWireAction(target.Owner, remoteUser, heldEntity.Value, message.Id, message.Action, target.Comp, tool);
    }
    #endregion
}
