using Content.Server.DeviceNetwork.Systems;
using Content.Shared.DeviceNetwork.Components;

namespace Content.Server._Starlight.UXN.Devices.ComponentDevices;

/// <summary>
/// A UXNDevice that allows communication to other UXN devices nearby
/// </summary>
public sealed partial class NetworkDevice : ComponentUxnDevice<DeviceNetworkComponent>
{
    public override string Id => "network";
    private DeviceNetworkSystem _deviceNetwork = default!;
    protected override void SetupCore(EntityUid euid, DeviceNetworkComponent comp)
    {
        var _entMan = IoCManager.Resolve<IEntitySystemManager>();
        _deviceNetwork = _entMan.GetEntitySystem<DeviceNetworkSystem>();
    }
}

public static class UxnDeviceNetworkConstants
{
    /// <summary>
    /// what is the Contents the UXN is transmitting. should be a IEnuerable<byte>
    /// </summary>
    public const string Contents = "contents";
}