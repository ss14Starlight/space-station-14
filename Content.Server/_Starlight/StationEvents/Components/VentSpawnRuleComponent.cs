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
    /// Location that was picked.
    /// </summary>
    [DataField]
    public MapCoordinates? Coords;
}
