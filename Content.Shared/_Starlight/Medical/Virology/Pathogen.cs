using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// A concrete strain. Unlike most content in this codebase these are built at runtime
/// rather than authored, because a strain has to be unknown to the crew until someone
/// diagnoses it - see <see cref="PathogenArchetypePrototype"/> for why.
///
/// Strains live in the registry for the length of a round and are referenced by
/// <see cref="Id"/> everywhere else.
/// </summary>
public sealed class Pathogen
{
    /// <summary>
    /// Round-unique. This is what infections and immunities store.
    /// </summary>
    public int Id;

    /// <summary>
    /// Rolled code such as "RH-882", so two strains from the same archetype are still
    /// distinguishable when the crew talks about them.
    /// </summary>
    public string Designation = string.Empty;

    public ProtoId<PathogenArchetypePrototype> Archetype;

    public PathogenType PathogenType = PathogenType.Virus;

    /// <summary>
    /// Which prevalence budget this draws from.
    /// </summary>
    public PathogenTier Tier = PathogenTier.Ambient;

    public bool Beneficial;

    /// <summary>
    /// How much partial PPE protection this erodes, from 0 to 1. Complete protection from
    /// working internals or purpose-built bio gear is never reduced.
    /// </summary>
    public float ProtectionBypass;

    public bool ImmunityOnRecovery = true;

    public bool RespawnOnExtinction;

    /// <summary>
    /// Largest share of living crew that can carry this at once.
    /// </summary>
    public float MaxPrevalence = 0.15f;

    public float Transmissibility;

    public float SpreadRange;

    public TimeSpan Incubation;

    public int MaxStage = 1;

    public TimeSpan StageDelay;

    /// <summary>
    /// <see cref="TimeSpan.Zero"/> means it never clears without treatment.
    /// </summary>
    public TimeSpan Duration;

    public List<ProtoId<PathogenSymptomPrototype>> Symptoms = new();

    /// <summary>
    /// Station-wide diagnosis progress for this runtime strain.
    /// </summary>
    public PathogenIdentificationStage Identification;

    /// <summary>
    /// Patient identities already used for analysis. This is intentionally server-only
    /// and is never exposed on swabs or detector readouts.
    /// </summary>
    public HashSet<EntityUid> SampledHosts = new();
}

public enum PathogenIdentificationStage : byte
{
    Unidentified,
    Partial,
    Complete,
}
