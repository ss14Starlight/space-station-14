using Content.Shared._Starlight.StationRadio;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.StationRadio.UI;

[UsedImplicitly]
public sealed class StationRadioReceiverBoundUserInterface : BoundUserInterface
{
    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(0.1);

    [Dependency] private IGameTiming _timing = default!;

    [ViewVariables]
    private StationRadioReceiverWindow? _window;

    private TimeSpan _lastSend;

    public StationRadioReceiverBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<StationRadioReceiverWindow>();
        _window.SetRadio(Owner);
        _window.OnPower += () => SendMessage(new StationRadioReceiverPowerMessage());
        _window.OnVolume += (volume, released) =>
        {
            var now = _timing.RealTime;
            if (!released && now - _lastSend < SendInterval)
                return;

            _lastSend = now;
            SendMessage(new StationRadioReceiverVolumeMessage(volume));
        };
    }
}
