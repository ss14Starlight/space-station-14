using Content.Shared.Arcade.SpaceVillain;
using Robust.Client.UserInterface;

namespace Content.Client.Arcade.UI;

public sealed class SpaceVillainArcadeBoundUserInterface : BoundUserInterface
{
    [ViewVariables] private SpaceVillainArcadeMenu? _menu;

    public SpaceVillainArcadeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        SendAction(SpaceVillainPlayerAction.RequestData); // Starlight-edit
    }

    public void SendAction(SpaceVillainPlayerAction action) // Starlight-edit
    {
        SendMessage(new SpaceVillainArcadePlayerActionMessage(action));
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SpaceVillainArcadeMenu>();
        _menu.OnPlayerAction += SendAction;
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is SpaceVillainArcadeDataUpdateMessage msg)
            _menu?.UpdateInfo(msg);
    }
}
