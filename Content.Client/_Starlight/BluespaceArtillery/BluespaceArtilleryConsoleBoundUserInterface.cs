using Content.Shared._Starlight.BluespaceArtillery;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.BluespaceArtillery;

public sealed class BluespaceArtilleryConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private BluespaceArtilleryConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BluespaceArtilleryConsoleWindow>();
        _window.OnBuild += () => SendMessage(new BluespaceArtilleryBuildMessage());
        _window.OnSelectTarget += target => SendMessage(new BluespaceArtillerySelectTargetMessage(target));
        _window.OnCharge += charge => SendMessage(new BluespaceArtilleryChargeMessage(charge));
        _window.OnFire += () => SendMessage(new BluespaceArtilleryFireMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BluespaceArtilleryConsoleUiState consoleState)
            _window?.UpdateState(consoleState);
    }
}
