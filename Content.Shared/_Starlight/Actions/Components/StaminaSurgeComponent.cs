using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StaminaSurgeComponent : Component
{
    [DataField, AutoNetworkedField] public EntProtoId Action = "StaminaSurge";
    [DataField] public float? StaminaRegenModifier = 1.85f;
    [DataField] public float? StaminaResistModifier = 0.85f;
    [DataField] public float? StaminaCooldownModifier = 0.55f;
    [DataField] public float? HungerDrain = 90f;
    [DataField] public float? ThirstDrain = 8f;
    [DataField] public ProtoId<AlertPrototype> SurgeAlert = "Surge";
    [DataField] public TimeSpan? Duration = TimeSpan.FromSeconds(20);
    [ViewVariables] public bool Active;
    /// <summary>
    /// When the effect is due to end.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public TimeSpan? EffectEndTime;
    /// <summary>
    /// The action entity associated with this component.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public EntityUid? ActionEntity;
}
