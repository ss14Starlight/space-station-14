using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Present on anything currently carrying one or more pathogens.
/// Removed automatically once the last infection clears.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenInfectionComponent : Component
{
    [DataField]
    public List<PathogenInfection> Infections = new();
}

/// <summary>
/// One active infection on one host.
/// </summary>
[DataDefinition]
public sealed partial class PathogenInfection
{
    /// <summary>
    /// Strain id, resolved through the registry. Strains are generated per round rather
    /// than authored, so this is an id rather than a prototype reference.
    /// </summary>
    [DataField(required: true)]
    public int Pathogen;

    /// <summary>
    /// Zero while incubating. Transmission is already active, but symptoms start at 1.
    /// </summary>
    [DataField]
    public int Stage;

    /// <summary>
    /// When the next stage increment happens. While <see cref="Stage"/> is zero this is
    /// the end of incubation.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextStage;

    /// <summary>
    /// When this clears on its own. <see cref="TimeSpan.Zero"/> means it never does.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan EndTime;

    /// <summary>
    /// Next expression time, keyed by symptom prototype id.
    /// </summary>
    //TODO: make this a datafield and (de)serialize values as time offsets when
    // https://github.com/space-wizards/RobustToolbox/issues/3768 is fixed.
    // Same limitation AutoEmoteComponent documents.
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, TimeSpan> SymptomTimers = new();

    /// <summary>
    /// Admin-test override for symptom cadence. Null uses each symptom prototype's
    /// normal interval.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? SymptomIntervalOverride;

    /// <summary>
    /// When this fungal infection next gets a roll to shed a reservoir patch. A carrier
    /// sheds continuously rather than once, so fungus can actually sustain a chain, but
    /// each opportunity is decided by luck rather than a fixed patch budget.
    /// </summary>
    [DataField]
    public TimeSpan NextSporePatch;
}
