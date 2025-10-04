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
    public bool LegendaryApplied;
    public bool RollProcessed;
}
