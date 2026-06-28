using System.Numerics;
using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.CustomSpawner;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CustomSpawnerComponent : Component
{
    /// <summary>
    /// List of spawn data for this spawner.
    /// Spawn process can be configured.
    /// </summary>
    [DataField] public List<CSpawnData> SpawnData = [];
    /// Maximum times this spawner can trigger.
    [DataField] public int MaxTriggers = -1;
    /// How many triggers this spawner has left before ceasing to function.
    [ViewVariables(VVAccess.ReadWrite)] public int TimesTriggered;
    /// <summary>
    /// Determines if the spawner will automatically spawn entities on an interval while <see cref="Enabled"/> is <see langword="true"/>.
    /// <br/>
    /// If <see langword="false"/>, can only spawn entities if triggered with a signal from another device.
    /// </summary>
    [DataField] public bool SpawnOnInterval;
    /// The interval to spawn entities at if <see cref="SpawnOnInterval"/> is <see langword="true"/>.
    [DataField] public TimeSpan? SpawnInterval;
    /// The next game time in which spawn will be triggered if <see cref="SpawnOnInterval"/> is <see langword="true"/>.
    [ViewVariables] public TimeSpan? NextSpawnTime;
    /// Disable after trigger if true.
    [DataField] public bool OneShot;
    /// <summary>
    /// Probability of the spawner successfully triggering at all.
    /// Unlike <see cref="CSpawnData.SpawnProb"/>, <see cref="TimesTriggered"/> will increment
    /// even if probability check fails.
    /// </summary>
    [DataField] public float TriggerProb = 1;
    /// Offset applied to spawned entities.
    [DataField] public Vector2 GlobalSpawnOffset;
    /// Rotation applied to spawned entities.
    [DataField] public float GlobalSpawnRotation;
    /// Strategy used for spawning entities.
    [DataField("strategy")] public SpawnStrategy SpawnStrategy = SpawnStrategy.All;
    /// Used for sequential spawning.
    [ViewVariables(VVAccess.ReadWrite)] public int SpawnIndex;
    /// Port for triggering a spawn.
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string TriggerSpawnPort = "TriggerSpawn";
    /// Port for turning the device on.
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string OnPort = "On";
    /// Port for turning the device off.
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string OffPort = "Off";
    /// Port for toggling the device.
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string TogglePort = "Toggle";
    /// Port for when a spawn is triggered.
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string SpawnTriggeredPort = "SpawnTriggered";
    /// Port for when the device is turned on.
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string EnabledPort = "Enabled";
    /// Port for when the device is turned off.
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string DisabledPort = "Disabled";
    /// If the spawner is enabled and can spawn entities or not.
    [DataField, AutoNetworkedField] public bool Enabled = true;
    /// Sprite specifier for the hologram to display at the spawner's position. Does nothing if <see langword="null"/>.
    [DataField, AutoNetworkedField] public SpriteSpecifier.Rsi? HologramSprite;
    /// Determines if the hologram is visible or not.
    [DataField, AutoNetworkedField] public bool HologramVisible;
    /// Also affects light color if light component is present.
    [DataField, AutoNetworkedField] public Color HologramColor1 = Color.White;
    [DataField, AutoNetworkedField] public Color HologramColor2 = Color.White;
    /// Reference to the hologram entity so that it can be updated.
    [ViewVariables, AutoNetworkedField] public EntityUid? HologramEntity;
    /// Prototype ID for the hologram entity.
    [DataField] public EntProtoId HologramProtoId;
    /// Prevents spawning the hologram entity entirely if <see langword="true"/>.
    [DataField] public bool IsMarker;
}

public enum SpawnStrategy : byte
{
    /// Spawn everything at once
    All,
    /// Spawn one at a time, in order.
    Sequential,
    /// Spawn randomly. Uses <see cref="CSpawnData.PickWeight"/> as the weight for each entry.
    Random
}
