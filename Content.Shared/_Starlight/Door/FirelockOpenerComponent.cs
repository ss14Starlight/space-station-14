using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Door;

/// <summary>
/// Lets mobs without complex interaction open and close firelocks by hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FirelockOpenerComponent : Component;
