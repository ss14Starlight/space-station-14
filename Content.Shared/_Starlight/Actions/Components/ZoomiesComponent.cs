using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZoomiesComponent : Component
{
    [DataField, AutoNetworkedField] public EntProtoId Action = "Zoomies";
    [DataField] public float? SpeedModifier = 1.35f;
    [DataField] public float? ThirstDrain = 120;
    [DataField] public float? HungerDrain = 12;
    [DataField] public TimeSpan? Duration = TimeSpan.FromSeconds(15);
    [DataField] public ProtoId<AlertPrototype> ZoomiesAlert = "Zoomies";
    [ViewVariables] public bool Active;
    [ViewVariables, AutoNetworkedField] public TimeSpan? EffectEndTime;
    [ViewVariables, AutoNetworkedField] public EntityUid? ActionEntity;
}
