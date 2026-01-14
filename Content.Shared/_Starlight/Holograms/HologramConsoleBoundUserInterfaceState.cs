using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Holograms;

[Serializable, NetSerializable]
public sealed class HologramConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<BladeServerInfo> BladeServers { get; init; } = new();
    public NetEntity? ActiveHologram { get; init; }
    public List<ProjectorInfo> Projectors { get; init; } = new();
    public Dictionary<NetEntity, NetCoordinates> ProjectorCoordinates { get; init; } = new();
    
    // Portable mode fields
    public bool IsPortable { get; init; }
    public float? BatteryPercent { get; init; }
    public bool AllowCarry { get; init; }
    public int ActiveCount { get; init; }
    public int MaxActive { get; init; }
    public int MaxBladeServerSlots { get; init; }
    public bool ShowMap { get; init; }
    public bool ShowProjectButton { get; init; }
    public bool ShowRecallButton { get; init; }
    public bool ShowBladeServerPanel { get; init; }
    public bool HasServer { get; init; }
    
    public HologramConsoleBoundUserInterfaceState(
        List<BladeServerInfo> bladeServers, 
        NetEntity? activeHologram, 
        List<ProjectorInfo> projectors,
        Dictionary<NetEntity, NetCoordinates> projectorCoordinates,
        bool isPortable = false,
        float? batteryPercent = null,
        bool allowCarry = false,
        int activeCount = 0,
        int maxActive = 0,
        int maxBladeServerSlots = 8,
        bool showMap = true,
        bool showProjectButton = true,
        bool showRecallButton = true,
        bool showBladeServerPanel = true,
        bool hasServer = true)
    {
        BladeServers = bladeServers;
        ActiveHologram = activeHologram;
        Projectors = projectors;
        ProjectorCoordinates = projectorCoordinates;
        IsPortable = isPortable;
        BatteryPercent = batteryPercent;
        AllowCarry = allowCarry;
        ActiveCount = activeCount;
        MaxActive = maxActive;
        MaxBladeServerSlots = maxBladeServerSlots;
        ShowMap = showMap;
        ShowProjectButton = showProjectButton;
        ShowRecallButton = showRecallButton;
        ShowBladeServerPanel = showBladeServerPanel;
        HasServer = hasServer;
    }
}

[Serializable, NetSerializable]
public sealed class BladeServerInfo
{
    public NetEntity Uid { get; init; }
    public string HologramName { get; init; }
    public bool IsActive { get; init; }
    
    public BladeServerInfo(NetEntity uid, string hologramName, bool isActive)
    {
        Uid = uid;
        HologramName = hologramName;
        IsActive = isActive;
    }
}

[Serializable, NetSerializable]
public sealed class ProjectorInfo
{
    public NetEntity Uid { get; init; }
    public string Name { get; init; }
    public string Location { get; init; }
    
    public ProjectorInfo(NetEntity uid, string name, string location)
    {
        Uid = uid;
        Name = name;
        Location = location;
    }
}

[Serializable, NetSerializable]
public sealed class HologramConsoleProjectHologramMessage : BoundUserInterfaceMessage
{
    public NetEntity BladeServerUid { get; }
    public NetEntity ProjectorUid { get; }
    
    public HologramConsoleProjectHologramMessage(NetEntity bladeServerUid, NetEntity projectorUid)
    {
        BladeServerUid = bladeServerUid;
        ProjectorUid = projectorUid;
    }
}

[Serializable, NetSerializable]
public sealed class HologramConsoleRecallMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class HologramConsoleEjectBladeServerMessage : BoundUserInterfaceMessage
{
    public NetEntity BladeServerUid { get; }
    
    public HologramConsoleEjectBladeServerMessage(NetEntity bladeServerUid) =>
        BladeServerUid = bladeServerUid;
}

[Serializable, NetSerializable]
public sealed class HologramConsoleToggleCarryMessage : BoundUserInterfaceMessage
{
    public bool AllowCarry { get; }
    
    public HologramConsoleToggleCarryMessage(bool allowCarry) =>
        AllowCarry = allowCarry;
}

[Serializable, NetSerializable]
public enum HologramConsoleUiKey : byte
{
    Key
}

