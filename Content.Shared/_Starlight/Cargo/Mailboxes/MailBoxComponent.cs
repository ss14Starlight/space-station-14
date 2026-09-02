using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Cargo.Mailboxes;

/// <summary>
/// This is used for mailboxes to allow mail to be put into.
/// </summary>
[RegisterComponent, AutoGenerateComponentState(fieldDeltas: true), NetworkedComponent]
public sealed partial class MailBoxComponent : Component
{
    /// <summary>
    /// The department that this mailbox belongs to.
    /// </summary>
    [DataField] public ProtoId<DepartmentPrototype> Department;

    /// <summary>
    /// Names of the people who have mail in this mailbox.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public HashSet<string> Names = new();

}
