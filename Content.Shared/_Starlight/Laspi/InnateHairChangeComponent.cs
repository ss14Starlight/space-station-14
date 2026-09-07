using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Laspi;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InnateHairChangeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionProto = "ActionInnateHairChange";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

[ByRefEvent]
public sealed partial class InnateHairChangeActionEvent : InstantActionEvent {}
