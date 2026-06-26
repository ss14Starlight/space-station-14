using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.CustomSpawner;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
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
    [ViewVariables(VVAccess.ReadWrite)] public int TriggersLeft = -1;
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
    /// Offset applied to spawned entities.
    [DataField] public Vector2 GlobalSpawnOffset;
    /// Strategy used for spawning entities.
    [DataField("strategy")] public SpawnStrategy SpawnStrategy = SpawnStrategy.All;
    /// Used for sequential spawning.
    [ViewVariables(VVAccess.ReadWrite)] public int SpawnIndex;
    /// If the spawner is enabled and can spawn entities or not.
    [DataField, AutoNetworkedField] public bool Enabled;
    /// Sprite specifier for the hologram to display at the spawner's position. Does nothing if <see langword="null"/>.
    [DataField, AutoNetworkedField] public SpriteSpecifier? HologramSprite;
    /// Determines if the hologram is visible or not.
    [DataField, AutoNetworkedField] public bool HologramVisible;
    /// Also affects light color if light component is present.
    [DataField, AutoNetworkedField] public Color HologramColor;
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
