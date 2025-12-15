using Robust.Shared.GameStates;

namespace Content.Shared.Roles.Components;

/// <summary>
/// Added to mind role entities to tag that they are an apprentice.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ApprenticeRoleComponent : Component;