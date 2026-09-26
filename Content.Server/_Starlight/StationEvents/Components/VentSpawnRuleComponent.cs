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
    public List<(MapCoordinates Coords, EntityUid Uid)> ValidLocations = new();

    /// <summary>
    /// Location that was picked.
    /// </summary>
    [DataField]
    public Dictionary<string, (MapCoordinates Coords, EntityUid Uid)> Vent = new();
}
