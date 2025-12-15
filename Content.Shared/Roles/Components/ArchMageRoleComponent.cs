using Robust.Shared.GameStates;

namespace Content.Shared.Roles.Components;

/// <summary>
/// Added to mind role entities to tag that they are an archmage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ArchMageRoleComponent : Component;