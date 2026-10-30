using Content.Shared._Starlight.StationRadio;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.StationRadio.UI;

[UsedImplicitly]
public sealed partial class StationRadioMixerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private static readonly TimeSpan _sendInterval = TimeSpan.FromSeconds(0.1);

    [Dependency] private IGameTiming _timing = default!;

    [ViewVariables]
    private StationRadioMixerWindow? _window;

    private TimeSpan _lastSend;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<StationRadioMixerWindow>();
        _window.OnFaderChanged += OnFaderChanged;
        _window.OnTransport += (source, action, slot) => SendMessage(new StationRadioMixerTransportMessage(source, action, slot));
    }

    private void OnFaderChanged(StationRadioMixerFader fader, NetEntity? source, float value, bool released)
    {
        var now = _timing.RealTime;
        if (!released && now - _lastSend < _sendInterval)
            return;

        _lastSend = now;
        SendMessage(new StationRadioMixerSetGainMessage(fader, source, value));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is StationRadioMixerState mixerState)
            _window?.UpdateState(mixerState);
    }
}
