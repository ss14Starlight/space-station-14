using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Computers.RemoteControl;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RemoteControlInterfaceTargetComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? RemoteBrain;
}
