using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// A template a concrete strain is rolled from when virology creates a disease.
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

    /// <summary>
    /// Analyzer description shown after the strain has been catalogued.
    /// </summary>
    [DataField]
    public LocId? Description;

    /// <summary>
    /// Broad family used by symptoms and contamination. Later spread and PPE systems also
    /// use this to choose the transmission route and protection checks.
    /// </summary>
    [DataField]
    public PathogenType PathogenType = PathogenType.Virus;

    /// <summary>
    /// Severity tier. Higher tiers can replace lower-tier active infections.
    /// </summary>
    [DataField]
    public PathogenTier Tier = PathogenTier.Ambient;

    /// <summary>
    /// Future weight for automatic ambient seeding. Zero means seeding code should only
    /// create it when something asks for it by name, which is how engineered strains work.
    /// </summary>
    [DataField]
    public float SeedWeight = 1f;

    /// <summary>
    /// Whether this archetype's effects are intended to help the host.
    /// </summary>
    [DataField]
    public bool Beneficial;

    /// <summary>
    /// Whether natural recovery grants immunity to this specific rolled strain.
    /// </summary>
    [DataField]
    public bool ImmunityOnRecovery = true;

    /// <summary>
    /// Whether this may respawn onto a random host when its last carrier loses it and
    /// the extinction respawn gate is enabled.
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

    /// <summary>
    /// Minimum rolled transmission strength. Stored and reported in PR A; later spread
    /// code consumes it as a multiplier.
    /// </summary>
    [DataField]
    public float MinTransmissibility = 0.03f;

    /// <summary>
    /// Maximum rolled transmission strength. Stored and reported in PR A; later spread
    /// code consumes it as a multiplier.
    /// </summary>
    [DataField]
    public float MaxTransmissibility = 0.06f;

    /// <summary>
    /// Minimum future spread radius in tiles.
    /// </summary>
    [DataField]
    public float MinSpreadRange = 1.5f;

    /// <summary>
    /// Maximum future spread radius in tiles.
    /// </summary>
    [DataField]
    public float MaxSpreadRange = 2f;

    /// <summary>
    /// Minimum time from infection to stage 1. Authored as seconds in YAML.
    /// </summary>
    [DataField]
    public TimeSpan MinIncubation = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum time from infection to stage 1. Authored as seconds in YAML.
    /// </summary>
    [DataField]
    public TimeSpan MaxIncubation = TimeSpan.FromSeconds(150);

    /// <summary>
    /// Minimum final stage a rolled strain can have, inclusive.
    /// </summary>
    [DataField]
    public int MinStages = 2;

    /// <summary>
    /// Maximum final stage a rolled strain can have, inclusive.
    /// </summary>
    [DataField]
    public int MaxStages = 3;

    /// <summary>
    /// Minimum time between stage increases after incubation. Authored as seconds in YAML.
    /// </summary>
    [DataField]
    public TimeSpan MinStageDelay = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Maximum time between stage increases after incubation. Authored as seconds in YAML.
    /// </summary>
    [DataField]
    public TimeSpan MaxStageDelay = TimeSpan.FromSeconds(180);

    /// <summary>
    /// Minimum time from stage 1 to natural recovery. Authored as seconds in YAML.
    /// Zero for both duration bounds means the strain never clears on its own.
    /// </summary>
    [DataField]
    public TimeSpan MinDuration = TimeSpan.FromMinutes(8);

    /// <summary>
    /// Maximum time from stage 1 to natural recovery. Authored as seconds in YAML.
    /// Zero for both duration bounds means the strain never clears on its own.
    /// </summary>
    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromMinutes(15);
}
