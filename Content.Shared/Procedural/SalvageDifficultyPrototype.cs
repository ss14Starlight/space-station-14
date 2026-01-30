using Robust.Shared.Prototypes;

#region Starlight
using Content.Shared.Procedural.Loot;
#endregion Starlight

namespace Content.Shared.Procedural;

[Prototype]
public sealed partial class SalvageDifficultyPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Color to be used in UI.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("color")]
    public Color Color = Color.White;

    /// <summary>
    /// How much loot this difficulty is allowed to spawn.
    /// </summary>
    [DataField("lootBudget", required : true)]
    public float LootBudget;

    /// <summary>
    /// How many mobs this difficulty is allowed to spawn.
    /// </summary>
    [DataField("mobBudget", required : true)]
    public float MobBudget;

    /// <summary>
    /// Budget allowed for mission modifiers like no light, etc.
    /// </summary>
    [DataField("modifierBudget")]
    public float ModifierBudget;

    [DataField("recommendedPlayers", required: true)]
    public int RecommendedPlayers;

    // Starlight Start

    [DataField]
    public TimeSpan Delay = TimeSpan.Zero;

    [DataField]
    public float Probability = 1;

    [DataField("lootPrototype")]
    public ProtoId<SalvageLootPrototype> LootPrototypeId = "SalvageLoot";
    // Starlight end
}
