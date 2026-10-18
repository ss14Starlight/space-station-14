using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Computers.RemoteControl;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RemoteControlControllerComponent : Component
{
    /// <summary>
    /// Consoles currently controlled by this entity.
    /// </summary>
    [AutoNetworkedField]
    public HashSet<EntityUid> Consoles = new();
}
