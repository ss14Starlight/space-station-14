using Content.Shared.Actions.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// This comp is added to station AI's invisible posibrain to handle shunting logics.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAIShuntableComponent : Component
{
    /// <summary>
    /// Action that allows the AI to shunt into another target.
    /// </summary>
    [DataField]
    public EntProtoId<ActionComponent> ShuntAction = "ActionAIShunt";

    /// <summary>
    /// Action that allows the AI to reconnect to its last shunt target.
    /// </summary>
    [DataField]
    public EntProtoId<ActionComponent> ReconnectShuntAction = "ActionAIReconnectShunt";

    /// <summary>
    /// what action is granted to the ai to allow them to return to their body.
    /// </summary>
    [DataField]
    public EntProtoId<ActionComponent> UnshuntAction = "ActionAIUnShunt";

    /// <summary>
    /// What body is this AI currently inhabiting, null if the AI is in it's core.
    /// </summary>
    [ViewVariables]
    public EntityUid? Inhabited = null;

    /// <summary>
    /// Station whose AI job slot this AI reserves while it is in an intellicard.
    /// </summary>
    [ViewVariables]
    public EntityUid? SlotReservationStation;

    [ViewVariables]
    public EntityUid? ShuntActionEntity;

    [ViewVariables]
    public EntityUid? ReconnectShuntActionEntity;
}
