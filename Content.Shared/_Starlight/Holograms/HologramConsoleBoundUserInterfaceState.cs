using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Holograms;

[Serializable, NetSerializable]
public sealed class HologramConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<DiskInfo> Disks { get; init; } = new();
    public NetEntity? ActiveHologram { get; init; }
    public List<ProjectorInfo> Projectors { get; init; } = new();
    public Dictionary<NetEntity, NetCoordinates> ProjectorCoordinates { get; init; } = new();
    
    // Portable mode fields
    public bool IsPortable { get; init; }
    public float? BatteryPercent { get; init; }
    public bool AllowCarry { get; init; }
    public int ActiveCount { get; init; }
    public int MaxActive { get; init; }
    public int MaxDiskSlots { get; init; }
    public bool ShowMap { get; init; }
    public bool ShowProjectButton { get; init; }
    public bool ShowRecallButton { get; init; }
    public bool ShowDiskPanel { get; init; }
    public bool HasServer { get; init; }
    
    public HologramConsoleBoundUserInterfaceState(
        List<DiskInfo> disks, 
        NetEntity? activeHologram, 
        List<ProjectorInfo> projectors,
        Dictionary<NetEntity, NetCoordinates> projectorCoordinates,
        bool isPortable = false,
        float? batteryPercent = null,
        bool allowCarry = false,
        int activeCount = 0,
        int maxActive = 0,
        int maxDiskSlots = 8,
        bool showMap = true,
        bool showProjectButton = true,
        bool showRecallButton = true,
        bool showDiskPanel = true,
        bool hasServer = true)
    {
        Disks = disks;
        ActiveHologram = activeHologram;
        Projectors = projectors;
        ProjectorCoordinates = projectorCoordinates;
        IsPortable = isPortable;
        BatteryPercent = batteryPercent;
        AllowCarry = allowCarry;
        ActiveCount = activeCount;
        MaxActive = maxActive;
        MaxDiskSlots = maxDiskSlots;
        ShowMap = showMap;
        ShowProjectButton = showProjectButton;
        ShowRecallButton = showRecallButton;
        ShowDiskPanel = showDiskPanel;
        HasServer = hasServer;
    }
}

[Serializable, NetSerializable]
public sealed class DiskInfo
{
    public NetEntity Uid { get; init; }
    public string HologramName { get; init; }
    public bool IsActive { get; init; }
    
    public DiskInfo(NetEntity uid, string hologramName, bool isActive)
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
    public NetEntity DiskUid { get; }
    public NetEntity ProjectorUid { get; }
    
    public HologramConsoleProjectHologramMessage(NetEntity diskUid, NetEntity projectorUid)
    {
        DiskUid = diskUid;
        ProjectorUid = projectorUid;
    }
}

[Serializable, NetSerializable]
public sealed class HologramConsoleRecallMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class HologramConsoleEjectDiskMessage : BoundUserInterfaceMessage
{
    public NetEntity DiskUid { get; }
    
    public HologramConsoleEjectDiskMessage(NetEntity diskUid) =>
        DiskUid = diskUid;
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

