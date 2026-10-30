namespace Content.Server._Starlight.Objectives.Components;

/// <summary>
/// Requires possession of an organic heart taken from the objective's target.
/// </summary>
[RegisterComponent]
public sealed partial class StealHeartConditionComponent : Component
{
    [DataField]
    public HashSet<EntityUid> Hearts = [];
}
