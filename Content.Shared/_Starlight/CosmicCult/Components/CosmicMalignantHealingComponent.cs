using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent]
public sealed partial class CosmicMalignantHealingComponent : Component
{
    /// <summary>
    /// The tile on which the entity is healed.
    /// </summary>
    [DataField]
    public ProtoId<ContentTileDefinition> HealingTile = "FloorCosmicCorruption";

    /// <summary>
    /// The amount of each damage type healed per healing interval.
    /// </summary>
    [DataField]
    public float HealAmount = 1f;

    /// <summary>
    /// The time between healing ticks.
    /// </summary>
    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(1);

    public TimeSpan NextHeal = TimeSpan.Zero;
}
