using Content.Shared.EntityEffects;
using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Shared._Starlight.EntityEffects.Effects.EntitySpawning;

/// <summary>
/// A type of <see cref="EntityEffectBase{T}"/> for effects that spawn entities by table.
/// </summary>
/// <inheritdoc cref="EntityEffect"/>
public sealed partial class SpawnEntityFromTable : EntityEffectBase<SpawnEntityFromTable>
{
    /// <summary>
    /// Amount of entities we're spawning.
    /// </summary>
    [DataField]
    public int Number = 1;

    /// <summary>
    /// Table from which we're pulling the entity to spawn.
    /// </summary>
    [DataField(required: true)]
    public EntityTableSelector EntityTable = default!;

    /// <summary>
    /// If set, how big the random offset is.
    /// </summary>
    [DataField]
    public float Offset = 0F;
}
