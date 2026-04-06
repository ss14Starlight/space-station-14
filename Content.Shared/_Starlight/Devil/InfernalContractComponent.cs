using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Devil;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class InfernalContractComponent : Component
{
    [AutoNetworkedField]
    public EntityUid Author;

    [AutoNetworkedField]
    public bool Completed = false;

    [AutoNetworkedField]
    public EntityUid? Signator = null;
}
