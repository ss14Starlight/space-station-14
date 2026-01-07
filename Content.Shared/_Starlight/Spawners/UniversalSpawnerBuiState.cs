using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.UniversalSpawner;

/// <summary>
/// State sent from server to client containing all spawner configuration
/// </summary>
[Serializable, NetSerializable]
public sealed class UniversalSpawnerBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<SpawnEntry> Entries { get; }
    public int MaxSpawns { get; }
    public float Offset { get; }
    public bool DeleteAfterSpawn { get; }
    public float SpawnChance { get; }
    public int MinSpawns { get; }
    public bool HasSpawned { get; }
    public List<string> AvailableHolidays { get; }
    public int MinRolls { get; }
    public int MaxRolls { get; }
    public SpawnerTriggerType TriggerType { get; }
    public float TriggerTimeSeconds { get; }
    public string? TriggerGameRule { get; }
    public float ProximityRange { get; }
    public List<string> AvailableGameRules { get; }

    public UniversalSpawnerBoundUserInterfaceState(
        List<SpawnEntry> entries,
        int maxSpawns,
        float offset,
        bool deleteAfterSpawn,
        float spawnChance,
        int minSpawns,
        bool hasSpawned,
        List<string> availableHolidays,
        int minRolls,
        int maxRolls,
        SpawnerTriggerType triggerType,
        float triggerTimeSeconds,
        string? triggerGameRule,
        float proximityRange,
        List<string> availableGameRules)
    {
        Entries = entries;
        MaxSpawns = maxSpawns;
        Offset = offset;
        DeleteAfterSpawn = deleteAfterSpawn;
        SpawnChance = spawnChance;
        MinSpawns = minSpawns;
        HasSpawned = hasSpawned;
        AvailableHolidays = availableHolidays;
        MinRolls = minRolls;
        MaxRolls = maxRolls;
        TriggerType = triggerType;
        TriggerTimeSeconds = triggerTimeSeconds;
        TriggerGameRule = triggerGameRule;
        ProximityRange = proximityRange;
        AvailableGameRules = availableGameRules;
    }
}

/// <summary>
/// Message to update the spawn entries
/// </summary>
[Serializable, NetSerializable]
public sealed class UniversalSpawnerUpdateEntriesMessage : BoundUserInterfaceMessage
{
    public List<SpawnEntry> Entries { get; }

    public UniversalSpawnerUpdateEntriesMessage(List<SpawnEntry> entries) =>
        Entries = entries;
}

/// <summary>
/// Message to update spawner settings
/// </summary>
[Serializable, NetSerializable]
public sealed class UniversalSpawnerUpdateSettingsMessage : BoundUserInterfaceMessage
{
    public int MaxSpawns { get; }
    public float Offset { get; }
    public bool DeleteAfterSpawn { get; }
    public float SpawnChance { get; }
    public int MinSpawns { get; }
    public int MinRolls { get; }
    public int MaxRolls { get; }
    public SpawnerTriggerType TriggerType { get; }
    public float TriggerTimeSeconds { get; }
    public string? TriggerGameRule { get; }
    public float ProximityRange { get; }

    public UniversalSpawnerUpdateSettingsMessage(
        int maxSpawns,
        float offset,
        bool deleteAfterSpawn,
        float spawnChance,
        int minSpawns,
        int minRolls,
        int maxRolls,
        SpawnerTriggerType triggerType,
        float triggerTimeSeconds,
        string? triggerGameRule,
        float proximityRange)
    {
        MaxSpawns = maxSpawns;
        Offset = offset;
        DeleteAfterSpawn = deleteAfterSpawn;
        SpawnChance = spawnChance;
        MinSpawns = minSpawns;
        MinRolls = minRolls;
        MaxRolls = maxRolls;
        TriggerType = triggerType;
        TriggerTimeSeconds = triggerTimeSeconds;
        TriggerGameRule = triggerGameRule;
        ProximityRange = proximityRange;
    }
}

/// <summary>
/// Message to manually trigger spawn
/// </summary>
[Serializable, NetSerializable]
public sealed class UniversalSpawnerTriggerMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Message to reset the spawner
/// </summary>
[Serializable, NetSerializable]
public sealed class UniversalSpawnerResetMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public enum UniversalSpawnerUiKey : byte
{
    Key
}
