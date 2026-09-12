using Content.Client.Eui;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Functional.TutorialServer.UI;

[UsedImplicitly]
public sealed class TutorialRolePickerEui : BaseEui
{
    private readonly TutorialRolePickerWindow _window;
    private bool _ignoreClose;

    public TutorialRolePickerEui()
    {
        _window = new TutorialRolePickerWindow();
        _window.RoleSelected += (roleId, confirmedStub) =>
        {
            // Server closes the EUI after select; suppress the window-close → CloseEuiMessage path.
            _ignoreClose = true;
            SendMessage(new TutorialSelectRoleMessage(roleId, confirmedStub));
            _window.Close();
        };
        _window.QuitPressed += () =>
        {
            _ignoreClose = true;
            SendMessage(new TutorialQuitPickerMessage());
            _window.Close();
        };
        // Without this, dismissing with X only closes the client window while the server keeps
        // the EUI open — Choose a tutorial then StateDirties a "still open" EUI and never shows UI.
        _window.OnClose += () =>
        {
            if (_ignoreClose)
                return;

            SendMessage(new CloseEuiMessage());
        };
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);
        if (state is not TutorialRolePickerEuiState pickerState)
            return;

        _window.Populate(pickerState.Roles);
        // Re-show if the client window was closed while the server EUI was still considered open.
        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    public override void Opened()
    {
        base.Opened();
        _ignoreClose = false;
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _ignoreClose = true;
        _window.Close();
    }
}
