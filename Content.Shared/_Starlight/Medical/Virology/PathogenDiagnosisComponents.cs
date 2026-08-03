using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Reads qualifying suit sensors for infection without exposing location data.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenDetectorComponent : Component;

[Serializable, NetSerializable]
public enum PathogenDetectorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PathogenDetectorUiState(
    List<PathogenDetectorEntry> infections,
    string contamination) : BoundUserInterfaceState
{
    public readonly List<PathogenDetectorEntry> Infections = infections;
    public readonly string Contamination = contamination;
}

[Serializable, NetSerializable]
public readonly record struct PathogenDetectorEntry(string Name, string Detection);

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
