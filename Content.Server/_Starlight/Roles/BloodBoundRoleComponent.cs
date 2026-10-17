using Content.Shared.Roles.Components;

namespace Content.Server._Starlight.Roles;

/// <summary>
/// Added to mind role entities to tag that they are a blood bound.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBoundRoleComponent : BaseMindRoleComponent
{
    [DataField]
    public EntityUid? Partner;
}
