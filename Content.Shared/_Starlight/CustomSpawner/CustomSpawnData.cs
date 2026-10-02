using System.Numerics;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Shared._Starlight.CustomSpawner;

[DataDefinition]
public sealed partial class CustomSpawnData
{
    /// Entity prototype to spawn.
    [DataField(required: true)] public EntProtoId ProtoId;
    /// List of possible component overrides.
    [DataField] public List<ComponentRegistry> Overrides = [];

    /// List of possible spawn offsets.
    [DataField] public List<Vector2> SpawnOffsets = [];
    /// List of possible spawn rotations in degrees.
    [DataField] public List<float> SpawnRotations = [];

    /// Enables applying an additional offset to the calculated one between a range of two positions.
    [DataField] public bool UseOffsetRange;
    /// Enables applying an additional offset to the calculated one between a range of two values.
    [DataField] public bool UseRotationRange;
    /// Minimum values for offset range. Think of it like the top left corner of a box.
    [DataField] public Vector2 OffsetRangeMin;
    /// Maximum values for offset range. Think of it like the bottom right corner of a box.
    [DataField] public Vector2 OffsetRangeMax;
    /// Minimum rotation value in degrees.
    [DataField] public float RotationRangeMin;
    /// Maximum rotation value in degrees.
    [DataField] public float RotationRangeMax;

    /// Likelihood of this entity being spawned at all.
    [DataField] public float SpawnProb = 1;
    /// Number of times this entity data can spawn. -1 is infinite. Does not increment per loop of <see cref="RepeatCount"/>.
    [DataField] public int MaxSpawns = -1;
    /// Number of spawns this entity data has left.
    [ViewVariables(VVAccess.ReadWrite)] public int TimesSpawned;

    /// Number of times to repeat spawn.
    [DataField] public int RepeatCount;
    /// Every entity spawned will use same offset instead of random.
    [DataField] public bool RepeatSameOffsets;
    /// Every entity spawned will use same rotation instead of random.
    [DataField] public bool RepeatSameRotations;
    /// Entity spawned will use the same component overrides instead of random.
    [DataField] public bool RepeatSameOverrides = true;

    /// Weight for being picked by the owning spawner's random selection.
    [DataField("weight")] public float PickWeight = 1;
}
