using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Roles.Components;

/// <summary>
/// Mind role marker for Vampires.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VampireRoleComponent : BaseMindRoleComponent;
