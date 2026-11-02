using Content.Shared.Roles.Components;

namespace Content.Server._Starlight.Roles;

/// <summary>
/// Added to mind role entities to tag that they are a blood bound.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBoundRoleComponent : BaseMindRoleComponent
{
    /// <summary>
    /// The partner's mind entity, so the oath survives body replacement or deletion.
    /// </summary>
    [DataField]
    public EntityUid? Partner;
}
