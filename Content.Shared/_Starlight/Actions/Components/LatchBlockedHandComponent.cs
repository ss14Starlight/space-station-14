using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Actions.Components;

/// <summary>
/// Marks the latch's blocking virtual item so it can't be dropped via the drop key.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LatchBlockedHandComponent : Component;
