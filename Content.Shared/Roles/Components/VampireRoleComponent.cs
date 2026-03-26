using Robust.Shared.GameStates;

namespace Content.Shared.Roles.Components;

/// <summary>
/// Mind role marker for Vampires.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VampireRoleComponent : BaseMindRoleComponent;
