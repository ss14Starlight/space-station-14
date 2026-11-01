using System.Numerics;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityEffects.Effects.EntitySpawning;

/// Spawns an entity on the effect target but with a coordinate offset. Supports overrides too.
public sealed partial class SpawnEntityWithOffsetEffect : EntityEffectBase<SpawnEntityWithOffsetEffect>
{
    [DataField(required: true)] public EntProtoId Entity;
    [DataField] public bool Predicted = true;
    [DataField] public int Amount = 1;
    [DataField] public float Angle;
    [DataField] public Vector2 Offset = Vector2.Zero;
    [DataField] public bool UseMapCoords;
    [DataField] public ComponentRegistry Overrides = [];
}
