using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Storage;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StomachPouchComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}
