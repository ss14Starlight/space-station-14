using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Stored on a tutorial practice grid: chamber centers and goal-gated doors.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialRoomLayoutComponent : Component
{
    /// <summary>
    /// World/grid-local center of each chamber (tile centers).
    /// </summary>
    [DataField]
    public List<Vector2> ChamberCenters = new();

    /// <summary>
    /// Inter-chamber gate doors. Index i unlocks when GoalIndex reaches i + 1.
    /// </summary>
    [DataField]
    public List<EntityUid> GateDoors = new();

    /// <summary>
    /// Marker id prefix for auto-spawned chamber entry pads (<c>chamber-{index}</c>).
    /// </summary>
    public const string ChamberEntryMarkerPrefix = "chamber-";

    public static string ChamberEntryMarkerId(int chamberIndex) => $"{ChamberEntryMarkerPrefix}{chamberIndex}";
}
