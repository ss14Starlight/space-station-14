using Content.Shared.Roles.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Implants.Components;

/// <summary>
/// Marker component for mind controlled players
/// </summary>
[RegisterComponent]
public sealed partial class MindControlComponent : BaseMindRoleComponent
{
    /// <summary>
    /// New Objective ID
    /// </summary>
    [DataField] public EntProtoId ObeyObjectiveId = "MindControlledFollowOrders";

    /// <summary>
    /// new Mind Role
    /// </summary>
    [DataField] public EntProtoId MindRoleId = "MindRoleMindControlled";
}
