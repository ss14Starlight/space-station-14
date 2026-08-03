using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Displays station contamination and qualifying sick crew without exposing crew locations.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenDetectorComponent : Component;

/// <summary>
/// Directly scans one nearby patient, contamination source, culture, or configured injector.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenAnalyzerComponent : Component
{
    [DataField]
    public TimeSpan ScanTime = TimeSpan.FromSeconds(0.8);
}

[Serializable, NetSerializable]
public enum PathogenAnalyzerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum PathogenAnalyzerTargetKind : byte
{
    Patient,
    ContaminationSource,
    Culture,
    Injector,
}

[Serializable, NetSerializable]
public sealed class PathogenAnalyzerUiState(
    string targetName,
    PathogenAnalyzerTargetKind targetKind,
    List<PathogenAnalyzerEntry> pathogens) : BoundUserInterfaceState
{
    public readonly string TargetName = targetName;
    public readonly PathogenAnalyzerTargetKind TargetKind = targetKind;
    public readonly List<PathogenAnalyzerEntry> Pathogens = pathogens;
}

[Serializable, NetSerializable]
public readonly record struct PathogenAnalyzerEntry(
    bool FullyIdentified,
    string Heading,
    string Context,
    string Classification,
    string Tier,
    string Origin,
    string Symptoms,
    string Incubation,
    string Duration,
    string Transmissibility,
    string ProtectionBypass,
    string MaxPrevalence);

[Serializable, NetSerializable]
public enum PathogenDetectorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PathogenDetectorUiState(
    NetEntity? grid,
    string stationName,
    float contamination,
    float virus,
    float bacteria,
    float fungus,
    List<string> sickCrew,
    List<PathogenContaminationBeaconGroup> groups) : BoundUserInterfaceState
{
    public readonly NetEntity? Grid = grid;
    public readonly string StationName = stationName;
    public readonly float Contamination = contamination;
    public readonly float Virus = virus;
    public readonly float Bacteria = bacteria;
    public readonly float Fungus = fungus;
    public readonly List<string> SickCrew = sickCrew;
    public readonly List<PathogenContaminationBeaconGroup> Groups = groups;
}

/// <summary>
/// A sterile, single-use swab. Filled specimen details stay server-side and anonymous.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenSwabComponent : Component
{
    [DataField]
    public TimeSpan SampleTime = TimeSpan.FromSeconds(2);

    [ViewVariables(VVAccess.ReadOnly)]
    public int Strain;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Host;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool SourceSample;

    public bool Filled => Strain > 0;
}

/// <summary>
/// A specimen transferred into a mini vial. Water and centrifuging make it analysable.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenSpecimenComponent : Component
{
    [DataField(required: true)]
    public int Strain;

    [DataField]
    public EntityUid? Host;

    [DataField]
    public bool SourceSample;

    [DataField]
    public bool Analysable;
}

/// <summary>
/// Physical feedstock produced when an analysis completes strain identification.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenViableCultureComponent : Component
{
    [DataField(required: true)]
    public int Strain;
}

[RegisterComponent]
public sealed partial class PathogenCentrifugeComponent : Component
{
    public const string ContainerId = "pathogen-culture-batch";

    [DataField]
    public TimeSpan ProcessTime = TimeSpan.FromSeconds(10);

    [DataField]
    public int Capacity = 6;

    [ViewVariables(VVAccess.ReadOnly)]
    public Container? Container;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Processing;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan FinishAt;
}

[RegisterComponent]
public sealed partial class PathogenDiagnoserComponent : Component
{
    [DataField]
    public TimeSpan AnalysisTime = TimeSpan.FromSeconds(5);
}

[Serializable, NetSerializable]
public sealed partial class PathogenSwabDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class PathogenDiagnoseDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class PathogenAnalyzerDoAfterEvent : SimpleDoAfterEvent;
