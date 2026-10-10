using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.UserInterface.Setup;

/// <summary>
/// This is used for marking an entity to be compatible with the setup system.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, ComponentProtoName("Setup")]
public sealed partial class SetupableComponent : Component
{
    [DataField, AutoNetworkedField] public bool NameSet;
}
