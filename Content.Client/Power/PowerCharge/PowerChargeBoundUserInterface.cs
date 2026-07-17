using Content.Shared._Sol.Shuttles.Components;
using Content.Shared.Power;
using Robust.Client.UserInterface;

namespace Content.Client.Power.PowerCharge;

public sealed class PowerChargeBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PowerChargeWindow? _window;

    public PowerChargeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    public void SetPowerSwitch(bool on)
    {
        SendMessage(new SwitchChargingMachineMessage(on));
    }

    protected override void Open()
    {
        base.Open();

        string title;
        if (EntMan.TryGetComponent(Owner, out PowerChargeComponent? component))
            title = Loc.GetString(component.WindowTitle);
        else if (EntMan.TryGetComponent(Owner, out StationAnchorTerminalComponent? terminal))
            title = Loc.GetString(terminal.WindowTitle);
        else
            return;

        _window = this.CreateWindow<PowerChargeWindow>();
        _window.UpdateWindow(this, title);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not PowerChargeState chargeState)
            return;

        _window?.UpdateState(chargeState);
    }
}
