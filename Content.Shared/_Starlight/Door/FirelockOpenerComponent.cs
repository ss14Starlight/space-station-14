using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Door;

/// <summary>
/// Lets an entity without complex interaction (e.g. corgis) click firelocks open
/// or closed, under the exact same rules as a humanoid's bare hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FirelockOpenerComponent : Component;
