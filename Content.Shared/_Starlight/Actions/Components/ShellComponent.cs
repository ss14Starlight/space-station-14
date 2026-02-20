using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.Components;

/// <summary>
/// Component that allows an entity to enter and exit stasis.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShellComponent : Component
{
    /// <summary>
    /// The entity needed to break your shell.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId BreakShellAction;

    [DataField, AutoNetworkedField]
    public EntityUid? BreakShellActionEntity;

    /// <summary>
    /// The entity needed to snap off a pice of your shell.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId GenerateShellPieceAction;

    [DataField, AutoNetworkedField]
    public EntityUid? GenerateShellPieceActionEntity;

    [DataField]
    public ComponentRegistry? NoShellComponents;
}
