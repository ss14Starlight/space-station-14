using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology;

/// <summary>
/// Data-driven pathogen definition used by the Sol virology system.
/// </summary>
[Prototype]
public sealed partial class PathogenPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = default!;

    [DataField]
    public LocId Description = "sol-pathogen-unknown-description";

    [DataField]
    public PathogenTransmission Transmission = PathogenTransmission.Contact;

    /// <summary>
    /// Minimum infectious dose required to establish infection.
    /// </summary>
    [DataField]
    public float InfectiveDose = 1f;

    /// <summary>
    /// Base chance (0-1) that a valid exposure establishes infection after dose and immunity checks.
    /// </summary>
    [DataField]
    public float BaseInfectionChance = 0.35f;

    [DataField]
    public TimeSpan IncubationDuration = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan SymptomaticDuration = TimeSpan.FromMinutes(8);

    [DataField]
    public TimeSpan CriticalDuration = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan RecoveryDuration = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Chance to die / go critical permanently if untreated in critical. Not used as death currently — drives damage intensity.
    /// </summary>
    [DataField]
    public float Lethality = 0.2f;

    [DataField]
    public DamageSpecifier SymptomaticDamage = new()
    {
        DamageDict = new()
        {
            { "Poison", 0.15 },
        },
    };

    [DataField]
    public DamageSpecifier CriticalDamage = new()
    {
        DamageDict = new()
        {
            { "Poison", 0.4 },
            { "Asphyxiation", 0.1 },
        },
    };

    /// <summary>
    /// Body temperature offset applied while symptomatic or critical (Kelvin).
    /// </summary>
    [DataField]
    public float FeverTemperatureOffset;

    [DataField]
    public float CoughChancePerSecond;

    [DataField]
    public float SneezeChancePerSecond;

    /// <summary>
    /// Organ slot identifiers that accumulate organ-targeted damage while infected.
    /// </summary>
    [DataField]
    public List<string> TargetOrgans = new();

    [DataField]
    public float OrganDamagePerSecond;

    /// <summary>
    /// Reagent IDs that reduce pathogen dose / advance recovery when metabolized.
    /// </summary>
    [DataField]
    public List<string> Treatments = new();

    /// <summary>
    /// Antibody / vaccine identity used for immunity matching.
    /// </summary>
    [DataField]
    public string VaccineIdentity = string.Empty;

    /// <summary>
    /// Environmental decay rate for surface/airborne load per second.
    /// </summary>
    [DataField]
    public float EnvironmentalDecayPerSecond = 0.01f;

    /// <summary>
    /// Multiplier for sterilant / disinfectant effectiveness against this pathogen.
    /// </summary>
    [DataField]
    public float SterilantSusceptibility = 1f;

    /// <summary>
    /// Duration of natural immunity after recovery.
    /// </summary>
    [DataField]
    public TimeSpan RecoveryImmunityDuration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Duration of vaccine-granted immunity.
    /// </summary>
    [DataField]
    public TimeSpan VaccineImmunityDuration = TimeSpan.FromHours(2);

    /// <summary>
    /// If true, this pathogen is only active on stations with VirologyStationComponent.
    /// </summary>
    [DataField]
    public bool RequiresVirologyStation = true;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ActivePathogenInfection
{
    [DataField]
    public string PathogenId = string.Empty;

    [DataField]
    public float Dose;

    [DataField]
    public PathogenStage Stage = PathogenStage.Incubation;

    [DataField]
    public TimeSpan StageStartedAt;

    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public bool FromSurgery;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class PathogenContaminationEntry
{
    [DataField]
    public string PathogenId = string.Empty;

    [DataField]
    public float Load;
}
