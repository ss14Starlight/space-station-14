using Content.Shared.Roles.Components;

namespace Content.Shared._Starlight.Admeme.Roles.Components;

/// <summary>
///     Added to the mind role entity <c>MindRoleNarsieCultist</c> to mark a mind as a Nar'Sie cultist.
///     Use <c>SharedRoleSystem.MindHasRole&lt;NarsieCultistRoleComponent&gt;</c> to query it.
/// </summary>
[RegisterComponent]
public sealed partial class NarsieCultistRoleComponent : BaseMindRoleComponent;
