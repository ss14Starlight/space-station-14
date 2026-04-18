using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class DockingInterfaceState
{
    /* Starlight BEGIN
    // public Dictionary<NetEntity, List<DockingPortState>> Docks;
    */ // Starlight END

    public DockingInterfaceState() // Starlight: -docks
    {
        // Docks = docks; // Starlight
    }
}
