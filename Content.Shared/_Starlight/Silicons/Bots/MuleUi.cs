using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Silicons.Bots;

[Serializable, NetSerializable]
public enum MuleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class MuleBoundUserInterfaceState(
    bool on,
    MuleMode mode,
    string? load,
    NetEntity? destination,
    NetEntity? home,
    Dictionary<NetEntity, string> beacons,
    bool autoReturn,
    bool autoPickup,
    bool report,
    float? charge) : BoundUserInterfaceState
{
    public readonly bool On = on;
    public readonly MuleMode Mode = mode;
    public readonly string? Load = load;
    public readonly NetEntity? Destination = destination;
    public readonly NetEntity? Home = home;
    public readonly Dictionary<NetEntity, string> Beacons = beacons;
    public readonly bool AutoReturn = autoReturn;
    public readonly bool AutoPickup = autoPickup;
    public readonly bool Report = report;
    public readonly float? Charge = charge;
}

[Serializable, NetSerializable]
public enum MuleCommand : byte
{
    Power,
    Stop,
    Go,
    Home,
    Unload,
    AutoReturn,
    AutoPickup,
    Report,
}

[Serializable, NetSerializable]
public sealed class MuleCommandMessage(MuleCommand command) : BoundUserInterfaceMessage
{
    public readonly MuleCommand Command = command;
}

[Serializable, NetSerializable]
public sealed class MuleSetDestinationMessage(NetEntity beacon, bool home) : BoundUserInterfaceMessage
{
    public readonly NetEntity Beacon = beacon;

    /// <summary>
    /// Sets home instead of the destination.
    /// </summary>
    public readonly bool Home = home;
}
