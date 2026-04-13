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
    protected override void SetupCore(EntityUid euid, DeviceNetworkComponent comp) => _deviceNetwork = _entSysMan.GetEntitySystem<DeviceNetworkSystem>();

    public void MakeEvent(UXNProcessor uxn, DeviceNetworkPacketEvent ev)
    {
        _readQueue.Enqueue(ev);
        uxn.PushEvent(new GenericVectorEvent(
            uxn.DevMem.GetShort(
                (byte)((uxn.SystemDevice.AttachedDevices[Id] << 0x4) + (byte)NetworkDeviceMemory.ReadVector)
                )
            )
         );
    }
}

public static class UxnDeviceNetworkConstants
{
    /// <summary>
    /// what is the Contents the UXN is transmitting. should be a IEnumerable<byte>
    /// </summary>
    public const string Contents = "contents";
}

public enum NetworkDeviceMemory : byte
{
    /// <summary>
    /// where will we return to upon recieving a packet.
    /// </summary>
    ReadVector = 0x0E
}
