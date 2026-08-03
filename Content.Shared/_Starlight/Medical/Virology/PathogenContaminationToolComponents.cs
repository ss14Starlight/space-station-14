using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical.Virology;

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
