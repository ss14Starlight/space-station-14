using Robust.Shared.GameStates;

namespace Content.Shared.Pinpointer;

/// <summary>
/// Used to indicate that an entity with the StationMapComponent should be updated to target the TargetStation of NukeopsRuleComponent.
/// Uses the most recent active NukeopsRule when spawned, or the NukeopsRule which spawned the grid that the entity is on.
/// </summary>
[RegisterComponent, NetworkedComponent] // Starlight: client needs this tag for the console's initial palette.
public sealed partial class NukeopsStationMapComponent : Component
{
}
