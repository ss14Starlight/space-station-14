using Content.Server.Power;
using Content.Shared.Electrocution;

// ReSharper disable CheckNamespace
namespace Content.Server.Wires;

public sealed partial class WiresSystem
{
    private void UpdateElectrificationWireAvailability(EntityUid uid, WiresComponent wires)
    {
        if (!TryComp<ElectrifiedComponent>(uid, out var electrified))
            return;

        var powerWireCount = 0;
        foreach (var wire in wires.WiresList)
        {
            if (wire.Action is not PowerWireAction)
                continue;

            powerWireCount++;
            if (powerWireCount >= 2)
                break;
        }

        var hasElectrifyingWire = powerWireCount >= 2;
        if (electrified.HasElectrifyingWire == hasElectrifyingWire)
            return;

        electrified.HasElectrifyingWire = hasElectrifyingWire;
        Dirty(uid, electrified);
    }
}
