using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent]
public sealed partial class CosmicMalignantHealingComponent : Component
{
    [DataField]
    public ProtoId<ContentTileDefinition> HealingTile = "FloorCosmicCorruption";

    [DataField]
    public float HealAmount = 1f;

    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(1);

    public TimeSpan NextHeal = TimeSpan.Zero;
}
