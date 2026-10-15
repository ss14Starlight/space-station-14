using Content.Shared.Chat.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.FireDragon;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FireDragonComponent : Component
{
    [DataField] public EntProtoId AncientFlameAction = "ActionFireDragonAncientFlame";
    [DataField] public EntProtoId TailSlamAction = "ActionFireDragonTailSlam";
    [DataField] public EntProtoId RoarAction = "ActionFireDragonRoar";
    [DataField] public EntProtoId WingDashAction = "ActionFireDragonWingDash";
    [DataField] public EntProtoId FirebreathAction = "ActionFireDragonFirebreath";
    [DataField] public EntityUid? AncientFlameActionEntity;
    [DataField] public EntityUid? TailSlamActionEntity;
    [DataField] public EntityUid? RoarActionEntity;
    [DataField] public EntityUid? WingDashActionEntity;
    [DataField] public EntityUid? FirebreathActionEntity;
    // Opt-in only while Wing Dash's movement still needs work.
    [DataField] public bool WingDashEnabled;

    [DataField] public SoundSpecifier? AbilitySound;

    [DataField] public EntProtoId Projectile = "ProjectileAncientFlame";
    [DataField] public EntProtoId GroundFirePrototype = "FireDragonGroundFire";
    [DataField] public EntProtoId HuntEffect = "StatusEffectPredatorHunt";
    [DataField] public ProtoId<EmotePrototype> RoarEmote = "FireDragonRoar";
    [DataField] public float AbilityRange = 12f;
    [DataField] public float ProjectileSpeed = 7f;
    [DataField] public float RoarRange = 7f;
    [DataField] public float RoarSpeedModifier = 0.95f;
    [DataField] public TimeSpan RoarDuration = TimeSpan.FromSeconds(8);
    [DataField] public float DashSpeed = 10f;
    [DataField] public TimeSpan DashDuration = TimeSpan.FromSeconds(0.45);
    [DataField] public int BreathWidth = 3;
    [DataField] public int BreathLength = 4;
    [DataField] public TimeSpan BreathRowInterval = TimeSpan.FromSeconds(0.25);
    [DataField] public float BreathStepDistance = 0.25f;
    [DataField] public TimeSpan WaveStartDelay = TimeSpan.FromSeconds(0.15);
    [DataField] public int SlamRadius = 2;
    [DataField] public TimeSpan SlamRingInterval = TimeSpan.FromSeconds(0.525);
    [DataField] public TimeSpan SlamFireLifetime = TimeSpan.FromSeconds(2);
    [DataField] public TimeSpan AbilityRecovery = TimeSpan.FromSeconds(3);
    [DataField] public TimeSpan NpcDecisionInterval = TimeSpan.FromSeconds(0.25);
    [DataField] public float NpcBreathRange = 4.5f;
    [DataField] public float NpcDashMinRange = 2f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextAbility;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextNpcAction;
}
