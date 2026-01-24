using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Legendary;

/// <summary>
///     Marks an entity as potentially becoming an "Legendary" when the map loads
/// </summary>
[RegisterComponent]
public sealed partial class LegendaryItemComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Chance = 0.05f;

    [DataField]
    public LocId? Description;

    [DataField]
    public ProtoId<StoryPrototype>? Story;
    [DataField]
    public List<ResPath> LegendarySprites = new();

    public bool LegendaryApplied;
    public bool RollProcessed;
    public bool PatronReferenceApplied;
}
