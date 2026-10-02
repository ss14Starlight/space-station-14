using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.InherentAction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InherentActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "ActionScream";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public bool IsEquipment = false;
}
