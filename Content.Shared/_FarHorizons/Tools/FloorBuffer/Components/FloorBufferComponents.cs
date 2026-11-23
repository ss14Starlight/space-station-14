using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Tools.FloorBuffer.Components;

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState]
public sealed partial class FloorBufferComponent : Component
{
    [AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public float SpeedReduction = 0.33f;

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleZamboni";

    [AutoNetworkedField]
    public EntityUid? ToggleActionEntity;
}
