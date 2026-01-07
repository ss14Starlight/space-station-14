using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.UniversalSpawner;

/// <summary>
/// Trigger type for the spawner
/// </summary>
[Serializable, NetSerializable]
public enum SpawnerTriggerType
{
    None = 0,
    MapInit = 1,
    TimeIntoRound = 2,
    Gamerule = 3,
    Proximity = 4
}

/// <summary>
/// Represents a single entry
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class SpawnEntry
{
    /// <summary>
    /// The entity prototype ID to spawn
    /// </summary>
    [DataField("id")]
    public string PrototypeId { get; set; } = string.Empty;

    /// <summary>
    /// The weight/chance of this entry being selected (relative to other entries)
    /// </summary>
    [DataField("weight")]
    public float Weight { get; set; } = 1.0f;

    /// <summary>
    /// Optional holiday requirement - if set, only spawns during this holiday
    /// </summary>
    [DataField("holiday")]
    public string? Holiday { get; set; }

    /// <summary>
    /// Whether this entry is enabled
    /// </summary>
    [DataField("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How many of the entry should be spawned on trigger.
    /// </summary>
    [DataField("quantity")]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Whether this entry can be selected multiple times in a single spawn trigger
    /// </summary>
    [DataField("allowMultiple")]
    public bool AllowMultiple { get; set; } = false;

    /// <summary>
    /// Additional offset for this specific entry (added to the spawner's main offset)
    /// </summary>
    [DataField("offset")]
    public float Offset { get; set; } = 0.0f;
}

/// <summary>
/// Component for a universal spawner that can be configured in-game by mappers
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UniversalSpawnerComponent : Component
{
    /// <summary>
    /// List of all spawn entries
    /// </summary>
    [DataField("entries"), AutoNetworkedField]
    public List<SpawnEntry> Entries { get; set; } = new();

    /// <summary>
    /// Maximum number of entities to spawn (0 = unlimited)
    /// </summary>
    [DataField("maxSpawns"), AutoNetworkedField]
    public int MaxSpawns { get; set; } = 1;

    /// <summary>
    /// Random offset range for spawned entities
    /// </summary>
    [DataField("offset"), AutoNetworkedField]
    public float Offset { get; set; } = 0.0f;

    /// <summary>
    /// Whether to delete the spawner after spawning
    /// </summary>
    [DataField("deleteAfterSpawn"), AutoNetworkedField]
    public bool DeleteAfterSpawn { get; set; } = false;

    /// <summary>
    /// Whether the spawner has already spawned
    /// </summary>
    [DataField("hasSpawned"), AutoNetworkedField]
    public bool HasSpawned { get; set; } = false;

    /// <summary>
    /// Chance multiplier (0.0-1.0) for spawn to occur at all
    /// </summary>
    [DataField("spawnChance"), AutoNetworkedField]
    public float SpawnChance { get; set; } = 1.0f;

    /// <summary>
    /// Minimum number of entities to spawn
    /// </summary>
    [DataField("minSpawns"), AutoNetworkedField]
    public int MinSpawns { get; set; } = 1;

    /// <summary>
    /// Minimum number of different entries (rolls) to select per spawn trigger
    /// </summary>
    [DataField("minRolls"), AutoNetworkedField]
    public int MinRolls { get; set; } = 1;

    /// <summary>
    /// Maximum number of different entries (rolls) to select per spawn trigger
    /// </summary>
    [DataField("maxRolls"), AutoNetworkedField]
    public int MaxRolls { get; set; } = 1;

    /// <summary>
    /// Trigger type for the spawner
    /// </summary>
    [DataField("triggerType"), AutoNetworkedField]
    public SpawnerTriggerType TriggerType { get; set; } = SpawnerTriggerType.MapInit;

    /// <summary>
    /// Time in seconds after round start to trigger (only used if TriggerType is TimeIntoRound)
    /// </summary>
    [DataField("triggerTimeSeconds"), AutoNetworkedField]
    public float TriggerTimeSeconds { get; set; } = 60.0f;

    /// <summary>
    /// Gamerule prototype ID to trigger on (only used if TriggerType is Gamerule)
    /// </summary>
    [DataField("triggerGameRule"), AutoNetworkedField]
    public string? TriggerGameRule { get; set; }
    
    /// <summary>
    /// Proximity range in tiles (only used if TriggerType is Proximity)
    /// </summary>
    [DataField("proximityRange"), AutoNetworkedField]
    public float ProximityRange { get; set; } = 5.0f;
}
