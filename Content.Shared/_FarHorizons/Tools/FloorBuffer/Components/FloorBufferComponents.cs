using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Tools.FloorBuffer.Components;

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState]
public sealed partial class FloorBufferComponent : Component
{
    /// <summary>
    /// Is component enabled?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = false;

    /// <summary>
    /// How fast should the user be when the floor buffer is enabled?
    /// </summary>
    [DataField]
    public float SpeedReduction = 0.33f;

    /// <summary>
    /// Solution container name
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string SolutionContainer = "default";

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleZamboni";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;
}