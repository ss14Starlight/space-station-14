using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Healing;

[RegisterComponent]
public sealed partial class HealFromTilesComponent : Component
{
    /// <summary>
    /// The tiles on which the entity can be healed.
    /// </summary>
    [DataField("tiles")]
    public List<ProtoId<ContentTileDefinition>> Tiles = new();

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
