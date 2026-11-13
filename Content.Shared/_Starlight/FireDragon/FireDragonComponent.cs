using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.FireDragon;

/// <summary>
/// Shared Roar configuration and ability recovery state for a controllable fire dragon.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FireDragonComponent : Component
{
    /// <summary>
    /// Action prototype used to apply Predator Hunt with a roar.
    /// </summary>
    [DataField, AutoNetworkedField] public EntProtoId RoarAction = "ActionFireDragonRoar";

    /// <summary>
    /// Granted Ancient Flame action entity.
    /// </summary>
    [DataField, AutoNetworkedField] public EntityUid? AncientFlameActionEntity;

    /// <summary>
    /// Granted Tail Slam action entity.
    /// </summary>
    [DataField, AutoNetworkedField] public EntityUid? TailSlamActionEntity;

    /// <summary>
    /// Granted Roar action entity.
    /// </summary>
    [DataField, AutoNetworkedField] public EntityUid? RoarActionEntity;

    /// <summary>
    /// Granted Wing Dash action entity, if the ability is enabled.
    /// </summary>
    [DataField, AutoNetworkedField] public EntityUid? WingDashActionEntity;

    /// <summary>
    /// Granted Firebreath action entity.
    /// </summary>
    [DataField, AutoNetworkedField] public EntityUid? FirebreathActionEntity;

    /// <summary>
    /// Status effect prototype applied by Roar.
    /// </summary>
    [DataField, AutoNetworkedField] public EntProtoId HuntEffect = "StatusEffectPredatorHunt";

    /// <summary>
    /// Radius in meters in which Roar can affect visible enemies.
    /// </summary>
    [DataField, AutoNetworkedField] public float RoarRange = 7f;

    /// <summary>
    /// Movement speed multiplier applied by Predator Hunt.
    /// </summary>
    [DataField, AutoNetworkedField] public float RoarSpeedModifier = 0.95f;

    /// <summary>
    /// Duration of the Predator Hunt status effect.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan RoarDuration = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Shared recovery time before another ability can be used.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan AbilityRecovery = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Time when the shared ability recovery ends.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan NextAbility;
}
