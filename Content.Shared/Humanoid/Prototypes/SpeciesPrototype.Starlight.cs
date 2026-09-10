using Content.Shared.DisplacementMap;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Prototypes;

public sealed partial class SpeciesPrototype : IPrototype
{
    /// <summary>
    /// Displacement applied to this species' base body sprites and compatible markings.
    /// </summary>
    [DataField]
    public ProtoId<DisplacementDataPrototype>? Displacement { get; private set; }

    /// <summary>
    /// Migration information for character profiles saved using an older species ID.
    /// </summary>
    [DataField]
    public SpeciesProfileMigration? ProfileMigration { get; private set; }
}


[DataDefinition]
public sealed partial class SpeciesProfileMigration
{
    /// <summary>
    /// Previous species prototype IDs that should be converted to this species.
    /// Keep every historical ID here so profiles belonging to inactive players
    /// can still be migrated in the future.
    /// </summary>
    [DataField]
    public HashSet<string> OldSpecies { get; private set; } = [];
}
