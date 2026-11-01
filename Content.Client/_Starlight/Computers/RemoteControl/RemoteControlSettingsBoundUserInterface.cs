using Content.Shared._Starlight.Computers.RemoteControl;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client._Starlight.Computers.RemoteControl;

[UsedImplicitly]
public sealed partial class RemoteControlSettingsBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private RemoteControlSettingsWindow? _window;

    protected override void Open()
    {
        if (IsOpened && _window is { Disposed: false })
            return;

        base.Open();
        _window = this.CreateWindow<RemoteControlSettingsWindow>();
        _window.ApplyPressed += OnApplyPressed;
        _window.ToggleControl += OnToggleControl;
        _window.OpenScreen += OnOpenScreen;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is RemoteControlSettingsBuiState settings && _window is { Disposed: false } window)
        {
            var controlling = settings.Controller is { } controller
                && EntMan.TryGetEntity(controller, out var controllerEntity)
                && controllerEntity == PlayerManager.LocalEntity;
            window.UpdateState(settings, controlling);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || _window is not { } window)
            return;

        window.ApplyPressed -= OnApplyPressed;
        window.ToggleControl -= OnToggleControl;
        window.OpenScreen -= OnOpenScreen;
        _window = null;
    }

    private void OnApplyPressed(string channelText)
        => SendMessage(new RemoteControlSetChannelMessage
        {
            Channel = channelText.Length == 0 ? null : channelText
        });

    private void OnToggleControl() => SendMessage(new RemoteControlToggleMessage());

    private void OnOpenScreen() => SendMessage(new RemoteControlOpenScreenMessage());
}
