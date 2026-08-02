using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
    /// A template a concrete strain is rolled from when an outbreak is seeded.
///
/// Strains are generated rather than authored so that identification stays meaningful.
/// A fixed strain is memorised after three or four shifts, after which nobody ever touches
/// the diagnoser again and cataloguing rewards knowledge the player already had. An
/// archetype keeps the character - a fast harmless respiratory virus is always that - while
/// leaving the specifics unknown until someone actually runs a sample.
///
/// Set a min and max to the same value for anything that should not vary.
/// </summary>
[Prototype]
public sealed partial class PathogenArchetypePrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Base name. Generated strains append a designation code, so this reads as
    /// "space cold RH-882".
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField]
    public PathogenType PathogenType = PathogenType.Virus;

    /// <summary>
    /// Which prevalence budget this draws from. Ambient strains yield to anything more
    /// serious, so an outbreak always has room to actually be an outbreak.
    /// </summary>
    [DataField]
    public PathogenTier Tier = PathogenTier.Ambient;

    /// <summary>
    /// Weight for automatic ambient seeding. Zero means it is never seeded on its own and only
    /// appears when something asks for it by name - which is how engineered strains work.
    /// </summary>
    [DataField]
    public float SeedWeight = 1f;

    [DataField]
    public bool Beneficial;

    /// <inheritdoc cref="Pathogen.ProtectionBypass"/>
    [DataField]
    public float ProtectionBypass;

    [DataField]
    public bool ImmunityOnRecovery = true;

    /// <summary>
    /// Whether this respawns onto a random host when its last carrier loses it.
    /// Ambient strains do; the station never really gets rid of a cold, it just develops
    /// herd immunity until there is nobody left to infect.
    /// </summary>
    [DataField]
    public bool RespawnOnExtinction;

    /// <summary>
    /// Largest share of living crew that can carry this at once.
    /// </summary>
    [DataField]
    public float MaxPrevalence = 0.15f;

    // --- Symptoms ---

    /// <summary>
    /// Always present. This is what keeps an archetype recognisable between rounds.
    /// </summary>
    [DataField]
    public List<ProtoId<PathogenSymptomPrototype>> CoreSymptoms = new();

    /// <summary>
    /// Drawn from at random to fill out the rest of the strain.
    /// </summary>
    [DataField]
    public List<ProtoId<PathogenSymptomPrototype>> SymptomPool = new();

    [DataField]
    public int MinExtraSymptoms;

    [DataField]
    public int MaxExtraSymptoms = 2;

    // --- Rolled numbers ---

    [DataField]
    public float MinTransmissibility = 0.03f;

    [DataField]
    public float MaxTransmissibility = 0.06f;

    [DataField]
    public float MinSpreadRange = 1.5f;

    [DataField]
    public float MaxSpreadRange = 2f;

    [DataField]
    public TimeSpan MinIncubation = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan MaxIncubation = TimeSpan.FromSeconds(150);

    [DataField]
    public int MinStages = 2;

    [DataField]
    public int MaxStages = 3;

    [DataField]
    public TimeSpan MinStageDelay = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan MaxStageDelay = TimeSpan.FromSeconds(180);

    /// <summary>
    /// Zero for both means the strain never clears on its own.
    /// </summary>
    [DataField]
    public TimeSpan MinDuration = TimeSpan.FromMinutes(8);

    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromMinutes(15);
}
