using Content.Shared.Silicons.Laws;
using Robust.Shared.GameStates;


namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// This means a AI can take it over and then shunt back into their old body.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAIShuntComponent : Component
{
    /// <summary>
    /// What Station-AI will we be returning to after un-shunting
    /// </summary>
    [ViewVariables]
    [DataField, AutoNetworkedField]
    public EntityUid? Return = null;

    /// <summary>
    /// Holds the euid of the action so we can delete it when shunting out.
    /// </summary>
    [ViewVariables]
    [DataField, AutoNetworkedField]
    public EntityUid? ReturnAction = null;

    /// <summary>
    /// what was the lawset of the chassis before the AI shunted into it.
    /// </summary>
    [ViewVariables]
    [DataField, AutoNetworkedField]
    public SiliconLawset? OldLawset = null;
}
