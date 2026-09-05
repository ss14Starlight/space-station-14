namespace Content.Shared._Starlight.Spawners.Components;

/// <summary>
/// Represents a "category" of entities for the quantity despawn component
/// </summary>
[RegisterComponent]
public sealed partial class QuantityDespawnCategoryComponent : Component
{
    /// <summary>
    /// What is the maximum number of entities that can be spawned at any one point?
    /// </summary>
    [DataField]
    public int MaxEntities = 1000;
}
