using Content.Server._Starlight.StationEvents.Events;
using Robust.Shared.Map;

namespace Content.Server._Starlight.StationEvents.Components;

/// <summary>
/// Component for spawning antags in vents at station.
/// Requires <c>AntagSelectionComponent</c>.
/// </summary>
[RegisterComponent, Access(typeof(VentSpawnRule))]
public sealed partial class VentSpawnRuleComponent : Component
{
    /// <summary>
    /// If true, we'll insert entity in selected vent after antag selection.
    /// </summary>
    [DataField]
    public bool InsertInVent = true;

    public List<(MapCoordinates Coords, EntityUid Uid)> ValidLocations = new();

    /// <summary>
    /// Location that was picked.
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, (MapCoordinates Coords, EntityUid Uid)> Vent = new();
}
