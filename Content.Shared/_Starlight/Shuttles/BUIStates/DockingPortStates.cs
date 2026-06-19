using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

/// <summary>
/// Wrapper for the dictionary of docking port states.
/// </summary>
[Serializable, NetSerializable]
public sealed class DockingPortStates(Dictionary<NetEntity, List<DockingPortState>> docks)
{
    public Dictionary<NetEntity, List<DockingPortState>> Docks = docks;
}
