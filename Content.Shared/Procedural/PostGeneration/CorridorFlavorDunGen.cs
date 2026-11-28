using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;

namespace Content.Shared.Procedural.PostGeneration;

/// <summary>
/// Spawns a line of entities along the walls of corridors, like for vending machines, lockers, and tables.
/// </summary>
public sealed partial class CorridorFlavorDunGen : IDunGenLayer
{
    /// <summary>
    /// Number of corridor tiles in each line. Each tile rolls the table once.
    /// </summary>
    [DataField]
    public int Length = 3;

    /// <summary>
    /// Chance for each eligible line to spawn.
    /// </summary>
    [DataField]
    public float Prob = 1f;

    [DataField]
    public bool IgnoreReserved;

    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Contents = new();
}
