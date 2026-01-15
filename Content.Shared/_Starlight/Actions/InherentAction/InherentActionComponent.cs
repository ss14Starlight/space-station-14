using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

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
