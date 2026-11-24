using Content.Server.Wires;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Wires;

// ReSharper disable CheckNamespace
namespace Content.Server.Silicons.StationAi;

public sealed partial class AiInteractWireAction
{
    [DataField("pulseTimeout")]
    private int _pulseTimeout = 30;

    public override void Update(Wire wire)
    {
        if (!IsPowered(wire.Owner))
            WiresSystem.TryCancelWireAction(wire.Owner, PulseTimeoutKey.Key);
    }

    private void AwaitPulseCancel(Wire wire)
    {
        if (wire.IsCut
            || !EntityManager.TryGetComponent<StationAiWhitelistComponent>(wire.Owner, out var whitelist))
        {
            return;
        }

        EntityManager.System<SharedStationAiSystem>()
            .SetWhitelistEnabled((wire.Owner, whitelist), true);
    }

    private enum PulseTimeoutKey : byte
    {
        Key
    }
}
