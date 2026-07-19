using Content.Shared.Damage;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sol.Medical.Allergy;

[Prototype]
public sealed partial class AllergyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Species that innately have this allergy. It is applied on spawn and shown
    /// auto-selected (and locked) in the character editor allergy tab.
    /// </summary>
    [DataField]
    public List<ProtoId<SpeciesPrototype>> InnateSpecies = new();

    [DataField(required: true)]
    public LocId Name = default!;

    [DataField]
    public LocId Description = "sol-allergy-unknown-description";

    /// <summary>
    /// Reagent IDs that trigger this allergy.
    /// </summary>
    [DataField]
    public List<string> TriggerReagents = new();

    /// <summary>
    /// Food/entity prototype IDs that trigger this allergy when ingested.
    /// </summary>
    [DataField]
    public List<EntProtoId> TriggerFoods = new();

    /// <summary>
    /// Food prototype roots whose descendants trigger this allergy.
    /// Use only for semantically uniform food families.
    /// </summary>
    [DataField]
    public List<EntProtoId> TriggerFoodRoots = new();

    [DataField]
    public AllergySeverity DefaultSeverity = AllergySeverity.Mild;

    [DataField]
    public DamageSpecifier MildDamage = new()
    {
        DamageDict = new() { { "Poison", 0.5 } },
    };

    /// <summary>
    /// Per-tick damage while a severe reaction is active. Tuned to outpace respirator recovery.
    /// </summary>
    [DataField]
    public DamageSpecifier SevereDamage = new()
    {
        DamageDict = new()
        {
            { "Poison", 2 },
            { "Asphyxiation", 6 },
        },
    };

    /// <summary>
    /// Per-tick damage for anaphylaxis. Quickly life-threatening without clearing the allergen
    /// and treating the reaction (epinephrine / airway support).
    /// </summary>
    [DataField]
    public DamageSpecifier AnaphylaxisDamage = new()
    {
        DamageDict = new()
        {
            { "Poison", 4 },
            { "Asphyxiation", 12 },
        },
    };

    [DataField]
    public bool CausesSneezing = true;

    [DataField]
    public bool CausesAnaphylaxis;
}

public enum AllergySeverity : byte
{
    Mild = 0,
    Moderate = 1,
    Severe = 2,
    Anaphylaxis = 3,
}
