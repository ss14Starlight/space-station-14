using Content.Server._Starlight.StationEvents.Events;
using Robust.Shared.Map;
using Starlight.NullLink.Attributes;

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

    /// <summary>
    /// Location that was picked.
    /// </summary>
    [DataField]
    public (MapCoordinates, EntityUid)? Vent = null;
}
