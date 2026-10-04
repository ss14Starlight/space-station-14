using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Computers.RemoteControl;

[RegisterComponent, NetworkedComponent]
public sealed partial class RemoteControlConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? RemoteBrain;

    [ViewVariables]
    public HashSet<EntityUid> Users = new();

    [ViewVariables]
    public EntityUid? Controller;

    [ViewVariables]
    public EntityUid? PreviousRelayEntity;

    [ViewVariables]
    public bool BorgActivatedByRemote;

    [DataField]
    public bool EnableRemoteView = true;

    [DataField]
    public bool CanForceRemoteControl;
}
