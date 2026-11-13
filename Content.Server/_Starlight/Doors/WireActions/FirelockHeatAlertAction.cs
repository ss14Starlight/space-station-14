using Content.Server.Wires;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Wires;

namespace Content.Server._Starlight.Doors.WireActions;

public sealed partial class FirelockHeatAlertAction : ComponentWireAction<FirelockComponent>
{
    public override string Name { get; set; } = "wire-name-door-heat-alert";
    public override Color Color { get; set; } = Color.IndianRed;
    public override object? StatusKey { get; } = AirlockWireStatus.FireAlertIndicator;

    public override StatusLightState? GetLightState(Wire wire, FirelockComponent component) =>
        component.Temperature ? StatusLightState.On : StatusLightState.Off;

    public override bool Cut(EntityUid user, Wire wire, FirelockComponent component)
    {
        component.HeatWireCut = true;
        component.Temperature = true;
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, FirelockComponent component)
    {
        component.HeatWireCut = false;
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, FirelockComponent component)
    {

    }
}
