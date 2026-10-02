using Content.Shared.Salvage.Expeditions.Modifiers;
using Robust.Shared.Prototypes;
using Content.Shared.Procedural;

namespace Content.Shared.Salvage.Expeditions;

[Prototype]
public sealed partial class SalvageFactionPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField("desc")] public LocId Description { get; private set; } = string.Empty;

    [ViewVariables(VVAccess.ReadWrite), DataField("entries", required: true)]
    public List<SalvageMobEntry> MobGroups = new();

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
