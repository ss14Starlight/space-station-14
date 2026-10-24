using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.StatusIcon;

/// <summary>
/// Fixed job for entities with no ID card (K9, Borg, etc). Above-head icon
/// and crew monitoring's name/job/department derive from this.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FixedJobIconComponent : Component
{
    /// <summary>
    /// The JobPrototype that the entity using this component is configured to use.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<JobPrototype> Job;
}
