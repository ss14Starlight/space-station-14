using Content.Shared._Starlight.TwistyCube;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.TwistyCube;

public sealed class TwistyCubeBoundUserInterface : BoundUserInterface
{
    [ViewVariables] private TwistyCubeMenu? _menu;

    public TwistyCubeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        SendMessage(new TwistyCubeActionMessage(TwistyCubeAction.RequestData));
    }

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

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is TwistyCubeStateMessage msg)
            _menu?.UpdateState(msg.State);
    }
}