using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Genetics;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared.Eye.Blinding.Components;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
[GeneticComponent]
public sealed partial class ThermalVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public EntProtoId EffectPrototype = "EffectThermalVision";
}

