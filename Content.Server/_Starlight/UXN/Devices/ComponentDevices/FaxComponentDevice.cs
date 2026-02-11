using Content.Server.Fax;
using Content.Shared.Fax.Components;

namespace Content.Server._Starlight.UXN.Devices.ComponentDevices;

public sealed class FaxComponentDevice : ComponentUxnDevice<FaxMachineComponent>
{
    private FaxSystem _fax;
    protected override void SetupCore(EntityUid entity, FaxMachineComponent component) {
        var _entMan = IoCManager.Resolve<EntitySystemManager>();
        _fax = _entMan.GetEntitySystem<FaxSystem>();
    } // We dont need any extra setup/information from the ent. but we do need the sytem

    public override void ReadValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc) => base.ReadValue(memTarget, deviceMem, proc);
    public override void WriteValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc) => base.WriteValue(memTarget, deviceMem, proc);
}
