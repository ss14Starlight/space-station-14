using Content.Shared.Salvage.Expeditions.Modifiers;
using Robust.Shared.Prototypes;
using Content.Shared.Procedural;
using Content.Shared.Storage; // Starlight

namespace Content.Shared.Salvage.Expeditions;

[Prototype]
public sealed partial class SalvageFactionPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField("desc")] public LocId Description { get; private set; } = string.Empty;

    [ViewVariables(VVAccess.ReadWrite), DataField("entries", required: true)]
    public List<SalvageMobEntry> MobGroups = new();

    #region Starlight
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
    #endregion

    // 🌟Starlight🌟
    [DataField("biomes")]
    public List<ProtoId<SalvageBiomeModPrototype>>? Biomes { get; private set; } = null;

    // 🌟Starlight🌟
    [ViewVariables(VVAccess.ReadWrite), DataField("difficulties", required: true)]
    public List<ProtoId<SalvageDifficultyPrototype>> Difficulties = [];

    /// <summary>
    /// Miscellaneous data for factions.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("configs")]
    public Dictionary<string, string> Configs = new();
}
