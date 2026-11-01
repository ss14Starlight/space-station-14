using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RemoteControlBrainComponent : Component
{
    [ViewVariables, DataField, AutoNetworkedField]
    public int Channel = -1;
}

public readonly record struct RemoteControlBrainChannelRegisteredEvent(EntityUid Brain, int Channel);

public readonly record struct RemoteControlBrainChannelUnregisteredEvent(EntityUid Brain, int Channel);
