namespace Content.Shared._Starlight.Evolving;

/// <summary>
/// Tracks progress toward an evolve objective condition.
/// Added to objective entities by <see cref="EntitySystems.SharedEvolvingSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class EvolveConditionComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EvolveType? ConditionType;
    [DataField("count"), ViewVariables(VVAccess.ReadWrite)]
    public int Count = 0;
}
