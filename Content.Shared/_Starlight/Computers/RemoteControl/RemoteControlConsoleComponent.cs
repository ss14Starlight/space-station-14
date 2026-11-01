using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Computers.RemoteControl;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RemoteControlConsoleComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? RemoteBrain;

    [ViewVariables, DataField, AutoNetworkedField]
    public int? Channel;

    [ViewVariables]
    public HashSet<EntityUid> Users = new();

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Controller;

    [ViewVariables]
    public EntityUid? PreviousRelayEntity;

    [ViewVariables]
    public bool BorgActivatedByRemote;

    /// <summary>
    /// Whether this console provides a full remote view instead of a compact control-only interface.
    /// </summary>
    [DataField]
    public bool EnableRemoteView = true;

    /// <summary>
    /// Control priority. Direct possession has a power of 50.
    /// </summary>
    [DataField]
    public int RemoteControlPower = 10;

    /// <summary>
    /// Restricts an intrinsic remote controller to its owning entity.
    /// </summary>
    [DataField]
    public bool OwnerOnly;

    /// <summary>
    /// Action the owner must have before using this remote controller.
    /// </summary>
    [DataField]
    public EntProtoId? RequiredAction;
}
