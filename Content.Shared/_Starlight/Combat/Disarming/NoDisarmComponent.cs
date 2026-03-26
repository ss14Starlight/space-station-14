using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Combat.Disarming;

/// <summary>
/// Prevents the entity this is attached to from being disarmed. Can be attached to
/// the item holder as well as the item itself.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NoDisarmComponent : Component
{
}