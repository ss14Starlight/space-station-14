using Content.Shared._Starlight.UserInterface.Setup;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.UI.Setup;

[UsedImplicitly]
public sealed class SetupMenuBoundUi(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables] private SetupMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SetupMenu>();

        _menu.OnNameConfirm += SendDeviceName;
    }

    private void SendDeviceName(string name) => SendMessage(new SetupSetName(name));

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not SetupBoundUiState cast) return;

        _menu.UpdateState(cast.Name, cast.NameDisabled);
    }
}
