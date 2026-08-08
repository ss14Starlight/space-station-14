using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// A template a concrete strain is rolled from when an outbreak is seeded.
///
/// Strains are generated rather than authored so that identification stays meaningful.
/// A fixed strain is memorised after a few shifts, after which nobody ever touches
/// the diagnoser again and cataloguing rewards knowledge the player already had. An
/// archetype keeps the character while leaving the specifics unknown until someone
/// actually runs a sample.
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
    //
    // A rolled strain takes one entry from StageOneSymptoms, two entries from
    // StageTwoSymptomPool by default, and every entry in StageThreeSymptoms. Each list is
    // expected to hold symptoms of its own stage; nothing enforces that at load, so
    // VirologyContentTests checks it instead.

    /// <summary>
    /// One is picked at random. Every strain shows a single early type-level warning, and
    /// which one it is changes between rounds so the tell cannot be memorised.
    /// </summary>
    [DataField]
    public List<ProtoId<PathogenSymptomPrototype>> StageOneSymptoms = new();

    /// <summary>
    /// The middle of the illness, drawn from at random. This is where one strain differs
    /// from the next.
    /// </summary>
    [DataField]
    public List<ProtoId<PathogenSymptomPrototype>> StageTwoSymptomPool = new();

    /// <summary>
    /// All of these are always present. Reaching the last stage of an archetype looks the
    /// same every time, which is what keeps it recognisable once it has been catalogued.
    /// </summary>
    [DataField]
    public List<ProtoId<PathogenSymptomPrototype>> StageThreeSymptoms = new();

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
