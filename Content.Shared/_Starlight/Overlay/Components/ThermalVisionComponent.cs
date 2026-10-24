using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Overlay.Components;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState(true)]
public sealed partial class ThermalVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public bool GrantToggleAction = true;

    [DataField]
    public EntProtoId EffectPrototype = "EffectThermalVision";

    [DataField, AutoNetworkedField]
    public bool UseAlternativeShader = false;
}
