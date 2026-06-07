using Content.Shared._Starlight.Fax;
using Content.Shared._Starlight.Fax.UI;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Fax.UI;

public sealed partial class FaxMachineConfigureBoundUserInterface : BoundUserInterface
{
    [ViewVariables] private FaxMachineConfigureWindow? _window;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public FaxMachineConfigureBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FaxMachineConfigureWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not FaxMachineConfigureState faxState)
            return;

        _window?.SetName(faxState.Name);
        _window?.SetGroupings(_prototypeManager.EnumeratePrototypes<FaxGroupPrototype>());

        if (faxState.Grouping is not null)
            _window?.SetSelectedGrouping(faxState.Grouping);
    }
}
