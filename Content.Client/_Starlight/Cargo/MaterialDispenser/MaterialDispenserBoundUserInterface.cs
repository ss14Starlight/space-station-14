using Content.Shared._Starlight.Cargo.MaterialDispenser;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Cargo.MaterialDispenser;

public sealed class MaterialDispenserBoundUserInterface : BoundUserInterface
{

    [ViewVariables]
    private MaterialDispenserWindow? _window;

    public MaterialDispenserBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MaterialDispenserWindow>();
        _window.SetEntity(Owner);

        _window.OnDepartmentSelected += department => SendPredictedMessage(new MaterialDispenserDepartmentSelected(department));
        _window.StorageEjectButton.OnPressed += _ => SendMessage(new MaterialDispenserModeChange(MaterialDispenserMode.Eject));
        _window.StorageTransferButton.OnPressed += _ => SendMessage(new MaterialDispenserModeChange(MaterialDispenserMode.Transfer));
        _window.OnAmountButton += (s, i, buffer) => SendMessage(new MaterialDispenserAmountButton(s, i, buffer));
        _window.EjectCrate.OnPressed += _ => SendMessage(new MaterialDispenserEjectCrate());

    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        var castState = (MaterialDispenserBoundUserInterfaceState) state;

        _window?.UpdateState(castState);
    }
}
