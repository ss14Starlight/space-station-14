using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sol.Medical.Virology;

/// <summary>
/// Bounded trait modifiers that can be cultured from environmental samples
/// and assembled into a custom runtime strain.
/// </summary>
[Prototype]
public sealed partial class PathogenTraitPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = default!;

    [DataField]
    public LocId Description = "sol-pathogen-trait-unknown-description";

    /// <summary>
    /// Synthesis budget cost. Total trait costs on a strain must not exceed the chassis budget.
    /// </summary>
    [DataField]
    public int BudgetCost = 1;

    [DataField]
    public List<ProtoId<PathogenTraitPrototype>> IncompatibleWith = new();

    [DataField]
    public PathogenTransmission AddTransmission = PathogenTransmission.None;

    [DataField]
    public float InfectiveDoseMultiplier = 1f;

    [DataField]
    public float InfectionChanceBonus;

    [DataField]
    public float IncubationMultiplier = 1f;

    [DataField]
    public float SymptomaticMultiplier = 1f;

    [DataField]
    public float LethalityBonus;

    [DataField]
    public float EnvironmentalPersistenceMultiplier = 1f;

    [DataField]
    public float SterilantSusceptibilityMultiplier = 1f;

    [DataField]
    public float CoughChanceBonus;

    [DataField]
    public float SneezeChanceBonus;

    [DataField]
    public List<string> AddTargetOrgans = new();

    [DataField]
    public float OrganDamageBonus;

    [DataField]
    public DamageSpecifier SymptomDamageBonus = new();
}
