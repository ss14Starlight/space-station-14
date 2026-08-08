using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// A concrete strain. These are built at runtime rather than authored, because
/// a strain has to be unknown to the crew until someone diagnoses it.
/// Strains live in the registry for the length of a round and are referenced by
/// <see cref="Id"/> everywhere else.
/// </summary>
public sealed class Pathogen
{
    public int Id; // Round-unique. This is what infections and immunities store.

    public string Designation = string.Empty; // Rolled code such as "RH-882"

    public ProtoId<PathogenArchetypePrototype> Archetype;

    public PathogenType PathogenType = PathogenType.Virus;

    public PathogenTier Tier = PathogenTier.Ambient;

    public bool Beneficial;

    public bool ImmunityOnRecovery = true;

    public bool RespawnOnExtinction;

    public float MaxPrevalence = 0.15f; // Largest share of living crew that can carry this at once.

    public float Transmissibility;

    public float SpreadRange;

    public TimeSpan Incubation;

    public int MaxStage = 1;

    public TimeSpan StageDelay;

    public TimeSpan Duration;

    public List<ProtoId<PathogenSymptomPrototype>> Symptoms = new();

    public PathogenIdentificationStage Identification; // Station-wide diagnosis progress for this runtime strain.

    public HashSet<EntityUid> SampledHosts = new(); // Patient identities already used for analysis.
}

public enum PathogenIdentificationStage : byte
{
    Unidentified,
    Partial,
    Complete,
}
