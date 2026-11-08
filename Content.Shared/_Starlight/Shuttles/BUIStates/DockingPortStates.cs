using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Shuttles.BUIStates;

/// <summary>
/// Wrapper for the dictionary of docking port states.
/// </summary>
[Serializable, NetSerializable]
public sealed class DockingPortStates(Dictionary<NetEntity, List<DockingPortState>> docks)
{
    public Dictionary<NetEntity, List<DockingPortState>> Docks = docks;
    public long Version;
    public long DeliveryId;
}

[Serializable, NetSerializable]
public sealed class DockingPortStatesAcknowledgedMessage : BoundUserInterfaceMessage
{
    public long Version;
    public long DeliveryId;

    public DockingPortStatesAcknowledgedMessage(long version, long deliveryId)
    {
        Version = version;
        DeliveryId = deliveryId;
    }
}
