using Content.Shared._Starlight.Fax.UI;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Fax.UI;

public sealed class FaxMachineEditBoundUserInterface : BoundUserInterface
{
    [ViewVariables] private FaxMachineEditWindow? _window;

    public FaxMachineEditBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FaxMachineEditWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not FaxMachineEditState faxState)
            return;

        _window?.SetName(faxState.Name);
        _window?.SetGroupings(faxState.Groupings);
    }
}