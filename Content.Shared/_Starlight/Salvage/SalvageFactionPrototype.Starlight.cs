// ReSharper disable CheckNamespace

using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Shared.Salvage.Expeditions;

public sealed partial class SalvageFactionPrototype
{
    /// <summary>
    /// Optional boss spawned once before the faction's budgeted mobs.
    /// </summary>
    [DataField]
    public EntProtoId? Megafauna;

    /// <summary>
    /// Additional supporting mobs placed after the budgeted mobs, without consuming their budget.
    /// </summary>
    [DataField]
    public List<EntitySpawnEntry> ExtraMobs = new();
}
