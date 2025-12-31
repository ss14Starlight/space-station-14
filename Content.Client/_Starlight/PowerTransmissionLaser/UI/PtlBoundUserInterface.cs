using Content.Shared._Starlight.PowerTransmissionLaser;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.PowerTransmissionLaser.UI;

[UsedImplicitly]
public sealed class PtlBoundUserInterface : BoundUserInterface
{
    private PtlWindow? _window;

    public PtlBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PtlWindow>();

        _window.EnabledButton.OnPressed += _ =>
        {
            var next = !_window.Enabled;
            _window.SetEnabled(next);
            SendPredictedMessage(new PtlSetEnabledMessage(next));
        };

        _window.PowerApplied += mw => SendPredictedMessage(new PtlSetPowerMessage(mw));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not PtlBoundUserInterfaceState cast)
            return;

        _window.SetEnabled(cast.Enabled);
        _window.SetBattery(cast.BatteryCurrentJoules, cast.BatteryMaxJoules);
        _window.SetPowerBounds(cast.MinPowerMw, cast.MaxPowerMw);
        _window.SetTargetPowerMw(cast.TargetPowerMw);
        _window.SetTotalSpesos(cast.TotalSpesosEarned);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Close();
        _window = null;
    }
}
