using Content.Shared._FarHorizons.Silicons.IPC;
using Robust.Client.UserInterface;

namespace Content.Client._FarHorizons.Silicons.IPC;

public sealed partial class IPCBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private IPCMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<IPCMenu>();
        _menu.SetEntity(Owner);

        _menu.BrainButtonPressed += () =>
        {
            SendMessage(new IPCEjectBrainBuiMessage());
        };

        /* Unsure why this thing is commented but... Lets keep it commented.
        _menu.NameChanged += name =>
        {
            SendMessage(new IPCSetNameBuiMessage(name));
        };
        */
    }

    // This is cringe
    // Sadly cringe is how this game runs
    // Eventually prediction for bloodstream will be fixed, and we will once again remove this and switch to 100% client side UI
    // Then something else will break and make me do this shit again
    // And so the cycle continues
    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_menu == null)
            return;

        if (message is not IPCHealthMessage cast)
            return;

        _menu.SetHealth(cast);
    }
}
