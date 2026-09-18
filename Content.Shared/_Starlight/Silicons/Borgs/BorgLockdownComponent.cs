using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// Present on a borg that is locked down. A locked down borg is deactivated exactly as if its power cell
/// had run out, except that its speech is unaffected, and it cannot reactivate until the lock down is lifted.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BorgLockdownComponent : Component;

/// <summary>
/// Sent when a player uses the borg BUI to toggle a borg's lock down.
/// </summary>
[Serializable, NetSerializable]
public sealed class BorgToggleLockdownBuiMessage : BoundUserInterfaceMessage;
