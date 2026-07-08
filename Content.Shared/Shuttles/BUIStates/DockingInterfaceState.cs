using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class DockingInterfaceState
{
    /* Starlight BEGIN
    public Dictionary<NetEntity, List<DockingPortState>> Docks;

    public DockingInterfaceState(Dictionary<NetEntity, List<DockingPortState>> docks)
    {
        Docks = docks;
    }
    */ // Starlight END
}
