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
    /// If the spawner is enabled and can spawn entities or not.
    [DataField, AutoNetworkedField] public bool Enabled = true;

    /// <summary>
    /// List of spawn data for this spawner.
    /// Spawn process can be configured.
    /// </summary>
    [DataField] public List<CustomSpawnData> SpawnData = [];

    /// Maximum times this spawner can trigger.
    [DataField] public int MaxTriggers = -1;
    /// How many triggers this spawner has left before ceasing to function.
    [ViewVariables(VVAccess.ReadWrite)] public int TimesTriggered;

    /// Strategy used for spawning entities.
    [DataField("strategy")] public SpawnStrategy SpawnStrategy = SpawnStrategy.All;
    /// Used for sequential spawning.
    [ViewVariables(VVAccess.ReadWrite)] public int SpawnIndex;
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
    /// Unlike <see cref="CustomSpawnData.SpawnProb"/>, <see cref="TimesTriggered"/> will increment
    /// even if probability check fails.
    /// </summary>
    [DataField] public float TriggerProb = 1;

    /// Offset applied to spawned entities.
    [DataField] public Vector2 GlobalSpawnOffset;
    /// Rotation applied to spawned entities.
    [DataField] public float GlobalSpawnRotation;

    /// Port for triggering a spawn.
    [DataField]
    public ProtoId<SinkPortPrototype> TriggerSpawnPort = "TriggerSpawn";
    /// Port for turning the device on.
    [DataField]
    public ProtoId<SinkPortPrototype> OnPort = "On";
    /// Port for turning the device off.
    [DataField]
    public ProtoId<SinkPortPrototype> OffPort = "Off";
    /// Port for toggling the device.
    [DataField]
    public ProtoId<SinkPortPrototype> TogglePort = "Toggle";
    /// Port for when a spawn is triggered.
    [DataField]
    public ProtoId<SourcePortPrototype> SpawnTriggeredPort = "SpawnTriggered";
    /// Port for when the device is turned on.
    [DataField]
    public ProtoId<SourcePortPrototype> EnabledPort = "Enabled";
    /// Port for when the device is turned off.
    [DataField]
    public ProtoId<SourcePortPrototype> DisabledPort = "Disabled";

    /// Sprite specifier for the hologram to display at the spawner's position. Does nothing if <see langword="null"/>.
    [DataField, AutoNetworkedField] public SpriteSpecifier.Rsi? HologramSprite;
    /// Prototype ID to use for hologram instead of a set sprite. Requires <see cref="UseProtoSprite"/> to be <see langword="true"/>.
    [DataField, AutoNetworkedField] public EntProtoId? HologramProtoSprite;
    /// Determines if <see cref="HologramProtoSprite"/> will be used instead of <see cref="HologramSprite"/>.
    [DataField, AutoNetworkedField] public bool UseProtoSprite;
    /// Determines if the hologram is visible or not.
    [DataField, AutoNetworkedField] public bool HologramVisible;
    /// Also affects light color if light component is present.
    [DataField, AutoNetworkedField] public Color HologramColor1 = Color.White;
    [DataField, AutoNetworkedField] public Color HologramColor2 = Color.White;
    [DataField, AutoNetworkedField] public Vector2 HologramOffset = Vector2.Zero;
    /// Reference to the hologram entity so that it can be updated.
    [ViewVariables, AutoNetworkedField] public EntityUid? HologramEntity;
    [DataField, AutoNetworkedField] public bool LightVisible = true;

    /// When <see langword="false"/>, skips trying to update spawnpad sprites.
    [DataField, AutoNetworkedField] public bool UpdatePadSprites = true;

    /// Prototype ID for the hologram entity.
    [DataField] public EntProtoId? HologramProtoId;

    /// Prevents spawning the hologram entity entirely if <see langword="true"/>.
    [DataField] public bool IsMarker;
}

public enum SpawnStrategy : byte
{
    /// Spawn everything at once
    All,
    /// Spawn one at a time, in order.
    Sequential,
    /// Spawn randomly. Uses <see cref="CustomSpawnData.PickWeight"/> as the weight for each entry.
    Random
}
