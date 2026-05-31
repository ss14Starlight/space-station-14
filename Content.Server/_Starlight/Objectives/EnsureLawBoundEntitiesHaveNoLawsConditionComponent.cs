using Content.Shared.Whitelist;

namespace Content.Server._Starlight.Objectives;

[RegisterComponent]
public sealed partial class EnsureLawBoundEntitiesHaveNoLawsConditionComponent : Component
{
    /// <summary>
    /// The number of entities that need to have no laws for the condition to be a success.
    /// </summary>
    [DataField]
    public int EntitiesToFree = 3;

    [DataField]
    public EntityWhitelist? LawEntityWhitelist;

    [DataField]
    public EntityWhitelist? LawEntityBlacklist;
}
