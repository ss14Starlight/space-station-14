using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Power.BluespaceHarvester;

[Serializable, NetSerializable]
public enum BluespaceHarvesterUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class BluespaceHarvesterSetLevelMessage : BoundUserInterfaceMessage
{
    public int Level;

    public BluespaceHarvesterSetLevelMessage(int level) => Level = level;
}

[Serializable, NetSerializable]
public sealed class BluespaceHarvesterPurchaseMessage : BoundUserInterfaceMessage
{
    public string PoolId;

    public BluespaceHarvesterPurchaseMessage(string poolId) => PoolId = poolId;
}

[Serializable, NetSerializable]
public sealed class BluespaceHarvesterUiState : BoundUserInterfaceState
{
    public int CurrentLevel;
    public int DesiredLevel;
    public int MaxLevel;
    public float CurrentPower;
    public float PowerForNextLevel;
    public float NetworkSupply;
    public int AvailablePoints;
    public int TotalPoints;
    public bool DangerousMode;
    public bool IsBlocked;
    public BluespaceHarvesterPoolEntry[] Pools;

    public BluespaceHarvesterUiState(
        int currentLevel,
        int desiredLevel,
        int maxLevel,
        float currentPower,
        float powerForNextLevel,
        float networkSupply,
        int availablePoints,
        int totalPoints,
        BluespaceHarvesterPoolEntry[] pools,
        bool isBlocked = false)
    {
        CurrentLevel = currentLevel;
        DesiredLevel = desiredLevel;
        MaxLevel = maxLevel;
        CurrentPower = currentPower;
        PowerForNextLevel = powerForNextLevel;
        NetworkSupply = networkSupply;
        AvailablePoints = availablePoints;
        TotalPoints = totalPoints;
        DangerousMode = currentLevel >= 7;
        IsBlocked = isBlocked;
        Pools = pools;
    }
}

[Serializable, NetSerializable]
public sealed class BluespaceHarvesterPoolEntry
{
    public string Id;
    public string NameKey;
    public int Cost;
    public bool Enabled;

    public BluespaceHarvesterPoolEntry(string id, string nameKey, int cost, bool enabled)
    {
        Id = id;
        NameKey = nameKey;
        Cost = cost;
        Enabled = enabled;
    }
}
