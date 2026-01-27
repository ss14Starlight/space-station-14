using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StaminaSurgeComponent : Component
{
    [DataField, AutoNetworkedField] public EntProtoId Action = "StaminaSurge";
    [DataField] public float? StaminaRegenModifier = 1.8f;
    [DataField] public float? StaminaResistModifier = 0.9f;
    [DataField] public float? StaminaCooldownModifier = 0.6f;
    [DataField] public float? HungerDrain = 80f;
    [DataField] public float? ThirstDrain = 10f;
    [DataField] public ProtoId<AlertPrototype> SurgeAlert = "Surge";
    [DataField] public TimeSpan? Duration = TimeSpan.FromSeconds(20);
    /// <summary>
    /// When the effect is due to end.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public TimeSpan? EffectEndTime; 
    /// <summary>
    /// The action entity associated with this component.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public EntityUid? ActionEntity;
}