using Content.Server.Wires;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Wires;

namespace Content.Server._Starlight.Doors.WireActions;

public sealed partial class FirelockAirAlertAction : ComponentWireAction<FirelockComponent>
{
    public override string Name { get; set; } = "wire-name-door-air-alert";
    public override Color Color { get; set; } = Color.Aquamarine;
    public override object? StatusKey { get; } = AirlockWireStatus.AirAlertIndicator;

    public override StatusLightState? GetLightState(Wire wire, FirelockComponent component) =>
        component.Pressure ? StatusLightState.On : StatusLightState.Off;

    public override bool Cut(EntityUid user, Wire wire, FirelockComponent component)
    {
        component.PressureWireCut = true;
        component.Pressure = true;
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, FirelockComponent component)
    {
        component.PressureWireCut = false;
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, FirelockComponent component)
    {

    }
}
