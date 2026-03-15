using Content.Server._Starlight.Fax;
using Content.Server._Starlight.UXN.Devices.Events;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.UXN.Devices.ComponentDevices;

/// <summary>
/// A UXNDevice that allows communication to other UXN devices nearby
/// </summary>
public sealed partial class NetworkDevice : ComponentUxnDevice<DeviceNetworkComponent>
{
    public override string Id => "network";
    private DeviceNetworkSystem _deviceNetwork = default!;
    private readonly Queue<DeviceNetworkPacketEvent> _readQueue = new();
    protected override void SetupCore(EntityUid euid, DeviceNetworkComponent comp)
    {
        var _entMan = IoCManager.Resolve<IEntitySystemManager>();
        _deviceNetwork = _entMan.GetEntitySystem<DeviceNetworkSystem>();
    }

    public void MakeEvent(UXNProcessor uxn, DeviceNetworkPacketEvent ev)
    {
        _readQueue.Enqueue(ev);
        uxn.PushEvent(new GenericVectorEvent(
            uxn.DevMem.GetShort(
                (byte)((uxn.SystemDevice.AttachedDevices[Id] << 0x4) + 0x0E)
                )
            )
         );
    }
}

public static class UxnDeviceNetworkConstants
{
    /// <summary>
    /// what is the Contents the UXN is transmitting. should be a IEnuerable<byte>
    /// </summary>
    public const string Contents = "contents";
}