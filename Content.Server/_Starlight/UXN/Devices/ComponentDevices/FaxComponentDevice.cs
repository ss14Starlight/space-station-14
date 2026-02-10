using Content.Shared.Fax.Components;

namespace Content.Server._Starlight.UXN.Devices.ComponentDevices;

public sealed class FaxComponentDevice : ComponentUxnDevice<FaxMachineComponent>
{
    protected override void SetupCore(EntityUid entity, FaxMachineComponent component) { } // We dont need any extra setup/information from the ent
}
