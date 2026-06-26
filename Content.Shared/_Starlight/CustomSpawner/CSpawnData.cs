using System.Numerics;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Shared._Starlight.CustomSpawner;

[DataDefinition]
public sealed partial class CSpawnData
{
    /// Entity prototype to spawn.
    [DataField(required: true)] public EntProtoId ProtoId;
    /// List of possible component overrides.
    [DataField] public List<ComponentRegistry> Overrides = [];
    /// List of possible spawn offsets.
    [DataField] public List<Vector2> SpawnOffsets = [];
    /// Likelihood of this entity being spawned at all.
    [DataField] public float SpawnProb = 1;
    /// Number of times this entity data can spawn. -1 is infinite.
    [DataField] public int MaxSpawns = -1;
    /// Number of spawns this entity data has left.
    [ViewVariables(VVAccess.ReadWrite)] public int SpawnsLeft = -1;
    /// Number of times to repeat spawn.
    [DataField] public int RepeatCount;
    /// Every entity spawned will use same offset instead of random.
    [DataField] public bool RepeatSameOffsets;
    /// Entity spawned will use the same component overrides instead of random.
    [DataField] public bool RepeatSameOverrides = true;
    /// Weight for being picked by the owning spawner's random selection.
    [DataField("weight")] public float PickWeight = 1;
}
