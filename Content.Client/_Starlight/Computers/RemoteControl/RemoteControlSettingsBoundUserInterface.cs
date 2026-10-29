using Content.Shared._Starlight.Computers.RemoteControl;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client._Starlight.Computers.RemoteControl;

[UsedImplicitly]
public sealed class RemoteControlSettingsBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private RemoteControlSettingsWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<RemoteControlSettingsWindow>();
        _window.ApplyPressed += OnApplyPressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is RemoteControlSettingsBuiState settings && _window is { Disposed: false } window)
            window.UpdateState(settings);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || _window is not { } window)
            return;

        window.ApplyPressed -= OnApplyPressed;
        _window = null;
    }

    private void OnApplyPressed(string channelText)
        => SendMessage(new RemoteControlSetChannelMessage
        {
            Channel = channelText.Length == 0 ? null : channelText
        });
}
