using Content.Shared._Starlight.TwistyCube;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.TwistyCube;

public sealed class TwistyCubeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables] private TwistyCubeMenu? _menu;
    
    /// <summary>
    /// Sends a TwistyCubeAction to the server, wrapped as a message.
    /// </summary>
    /// <param name="action">The action to send</param>
    public void SendAction(TwistyCubeAction action)
    {
        SendMessage(new TwistyCubeActionMessage(action));
    }
    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<TwistyCubeMenu>();
        _menu.OnAction += SendAction;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is TwistyCubeBoundUserInterfaceState twistState)
            _menu?.UpdateState(twistState.State);
    }
}