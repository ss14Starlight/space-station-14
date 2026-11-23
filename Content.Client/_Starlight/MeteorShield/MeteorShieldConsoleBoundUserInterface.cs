using Content.Shared._Starlight.MeteorShield;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.MeteorShield;

public sealed class MeteorShieldConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private MeteorShieldConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<MeteorShieldConsoleWindow>();
        _window.OnToggle += satellite => SendMessage(new MeteorShieldToggleMessage(satellite));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is MeteorShieldConsoleUiState consoleState)
            _window?.UpdateState(consoleState);
    }
}
