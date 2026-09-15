using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Laspi;

/// <summary>
///    Component that allows an entity to change their hair/facial hair using the magic mirror UI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InnateHairChangeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionProto = "ActionInnateHairChange";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

/// <summary>
///   Event raised when the InnateHairChange action is used. it's handled by the InnateHairChangeSystem to open the magic mirror UI so you can look pretty.
/// </summary>
[ByRefEvent]
public sealed partial class InnateHairChangeActionEvent : InstantActionEvent {}
