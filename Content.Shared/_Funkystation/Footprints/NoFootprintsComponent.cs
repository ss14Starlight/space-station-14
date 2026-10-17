using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.Footprints;

/// <summary>
/// When placed on an entity or an entity's equipped shoes,
/// prevents them from leaving behind footprints.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NoFootprintsComponent : Component
{
}
