using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Displays station contamination and points toward the strongest active source.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenContaminationScannerComponent : Component;

[Serializable, NetSerializable]
public enum PathogenContaminationScannerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PathogenContaminationScannerUiState(
    NetEntity grid,
    string stationName,
    List<PathogenContaminationBeaconGroup> groups) : BoundUserInterfaceState
{
    public readonly NetEntity Grid = grid;
    public readonly string StationName = stationName;
    public readonly List<PathogenContaminationBeaconGroup> Groups = groups;
}

[Serializable, NetSerializable]
public readonly record struct PathogenContaminationBeaconGroup(
    NetEntity Beacon,
    NetCoordinates Coordinates,
    string BeaconName,
    float Total,
    float Bacteria,
    float Fungus,
    int SourceCount,
    int InfectiousSourceCount);

/// <summary>
/// Suppresses one active contamination source.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenDecontaminatorComponent : Component
{
    [DataField]
    public TimeSpan SuppressionDuration = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextUse;
}
