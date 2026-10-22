using Content.Shared.Mind;
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    /// <summary>
    /// Default alignment when this job's mind has no active antagonist or special role.
    /// </summary>
    [DataField]
    public ProtoId<RoleTypePrototype> RoleType = "Neutral";
}
