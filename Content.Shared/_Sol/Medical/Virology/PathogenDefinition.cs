using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology;

/// <summary>
/// Resolved pathogen data used by runtime systems. May come from a prototype chassis
/// or a round-scoped synthesized strain.
/// </summary>
[Serializable, NetSerializable]
public sealed class PathogenDefinition
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public string ChassisId = string.Empty;
    public bool IsRuntimeStrain;
    public PathogenTransmission Transmission = PathogenTransmission.Contact;
    public float InfectiveDose = 1f;
    public float BaseInfectionChance = 0.35f;
    public TimeSpan IncubationDuration = TimeSpan.FromMinutes(3);
    public TimeSpan SymptomaticDuration = TimeSpan.FromMinutes(8);
    public TimeSpan CriticalDuration = TimeSpan.FromMinutes(5);
    public TimeSpan RecoveryDuration = TimeSpan.FromMinutes(2);
    public float Lethality = 0.2f;
    public DamageSpecifier SymptomaticDamage = new();
    public DamageSpecifier CriticalDamage = new();
    public float FeverTemperatureOffset;
    public float CoughChancePerSecond;
    public float SneezeChancePerSecond;
    public List<string> TargetOrgans = new();
    public float OrganDamagePerSecond;
    public List<string> Treatments = new();
    public string VaccineIdentity = string.Empty;
    public float EnvironmentalDecayPerSecond = 0.01f;
    public float SterilantSusceptibility = 1f;
    public TimeSpan RecoveryImmunityDuration = TimeSpan.FromMinutes(30);
    public TimeSpan VaccineImmunityDuration = TimeSpan.FromHours(2);
    public bool RequiresVirologyStation = true;
    public List<string> TraitIds = new();

    public static PathogenDefinition FromPrototype(PathogenPrototype proto)
    {
        return new PathogenDefinition
        {
            Id = proto.ID,
            DisplayName = Loc.GetString(proto.Name),
            ChassisId = proto.ID,
            IsRuntimeStrain = false,
            Transmission = proto.Transmission,
            InfectiveDose = proto.InfectiveDose,
            BaseInfectionChance = proto.BaseInfectionChance,
            IncubationDuration = proto.IncubationDuration,
            SymptomaticDuration = proto.SymptomaticDuration,
            CriticalDuration = proto.CriticalDuration,
            RecoveryDuration = proto.RecoveryDuration,
            Lethality = proto.Lethality,
            SymptomaticDamage = new DamageSpecifier(proto.SymptomaticDamage),
            CriticalDamage = new DamageSpecifier(proto.CriticalDamage),
            FeverTemperatureOffset = proto.FeverTemperatureOffset,
            CoughChancePerSecond = proto.CoughChancePerSecond,
            SneezeChancePerSecond = proto.SneezeChancePerSecond,
            TargetOrgans = new List<string>(proto.TargetOrgans),
            OrganDamagePerSecond = proto.OrganDamagePerSecond,
            Treatments = new List<string>(proto.Treatments),
            VaccineIdentity = string.IsNullOrEmpty(proto.VaccineIdentity) ? proto.ID : proto.VaccineIdentity,
            EnvironmentalDecayPerSecond = proto.EnvironmentalDecayPerSecond,
            SterilantSusceptibility = proto.SterilantSusceptibility,
            RecoveryImmunityDuration = proto.RecoveryImmunityDuration,
            VaccineImmunityDuration = proto.VaccineImmunityDuration,
            RequiresVirologyStation = proto.RequiresVirologyStation,
        };
    }
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class RuntimePathogenStrain
{
    [DataField]
    public string StrainId = string.Empty;

    [DataField]
    public string Codename = string.Empty;

    [DataField]
    public ProtoId<PathogenPrototype> ChassisId;

    [DataField]
    public List<ProtoId<PathogenTraitPrototype>> Traits = new();

    [DataField]
    public TimeSpan CreatedAt;

    [DataField]
    public NetEntity? Creator;
}
