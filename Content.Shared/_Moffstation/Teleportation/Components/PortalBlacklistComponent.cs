using Robust.Shared.GameStates;

namespace Content.Shared._Moffstation.Teleportation.Components;

/// <summary>
///     Prevents this entity from being teleported by portals.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PortalBlacklistComponent : Component;
