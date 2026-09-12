using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marker on the dragon tutorial prey station so a ground pinpointer can lock onto it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialDragonPreyBeaconComponent : Component;
