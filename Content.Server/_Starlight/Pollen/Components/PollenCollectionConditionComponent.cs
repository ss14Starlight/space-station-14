namespace Content.Server._Starlight.Pollen.Components;

/// <summary>
/// Objective condition: track a Diona's progress collecting their randomly
/// assigned pollen plants. Progress and description are read live from the
/// Diona's own PollenCollectorComponent - nothing is mirrored here.
/// </summary>
[RegisterComponent]
public sealed partial class PollenCollectionConditionComponent : Component
{
    /// <summary>
    /// The Diona this objective tracks. Set once in RequirementCheckEvent.
    /// </summary>
    [DataField]
    public EntityUid? Diona;
}
