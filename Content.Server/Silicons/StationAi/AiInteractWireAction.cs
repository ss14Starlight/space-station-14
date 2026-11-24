using Content.Server.Wires;
using Content.Shared.Doors;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Wires;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Controls whether an AI can interact with the target entity.
/// </summary>
public sealed partial class AiInteractWireAction : ComponentWireAction<StationAiWhitelistComponent>
{
    public override string Name { get; set; } = "wire-name-ai-act-light";
    public override Color Color { get; set; } = Color.DeepSkyBlue;
    public override object StatusKey => AirlockWireStatus.AiControlIndicator;

    public override StatusLightState? GetLightState(Wire wire, StationAiWhitelistComponent component)
    {
        // Starlight - start
        if (wire.IsCut)
            return StatusLightState.Off;

        return component.Enabled ? StatusLightState.On : StatusLightState.BlinkingSlow;
        // Starlight - end
    }

    public override bool Cut(EntityUid user, Wire wire, StationAiWhitelistComponent component)
    {
        // Starlight - start
        WiresSystem.TryCancelWireAction(wire.Owner, PulseTimeoutKey.Key);
        EntityManager.System<SharedStationAiSystem>()
            .SetWhitelistEnabled((wire.Owner, component), false, announce: true);
        return true;
        // Starlight - end
    }

    public override bool Mend(EntityUid user, Wire wire, StationAiWhitelistComponent component)
    {
        return EntityManager.System<SharedStationAiSystem>()
            .SetWhitelistEnabled((wire.Owner, component), true);
    }

    public override void Pulse(EntityUid user, Wire wire, StationAiWhitelistComponent component)
    {
        // Starlight - start
        EntityManager.System<SharedStationAiSystem>()
            .SetWhitelistEnabled((wire.Owner, component), false);
        WiresSystem.StartWireAction(
            wire.Owner,
            _pulseTimeout,
            PulseTimeoutKey.Key,
            new TimedWireEvent(AwaitPulseCancel, wire));
        // Starlight - end
    }
}
