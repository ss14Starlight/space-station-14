using Content.Shared.Chat.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.FireDragon;

/// <summary>
/// Server-only fire attacks, optional dash movement, and NPC ability configuration.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FireDragonAbilitiesComponent : Component
{
    /// <summary>
    /// Action prototype used to launch Ancient Flame.
    /// </summary>
    [DataField] public EntProtoId AncientFlameAction = "ActionFireDragonAncientFlame";

    /// <summary>
    /// Action prototype used for Tail Slam.
    /// </summary>
    [DataField] public EntProtoId TailSlamAction = "ActionFireDragonTailSlam";

    /// <summary>
    /// Action prototype used for the optional Wing Dash.
    /// </summary>
    [DataField] public EntProtoId WingDashAction = "ActionFireDragonWingDash";

    /// <summary>
    /// Action prototype used for advancing firebreath.
    /// </summary>
    [DataField] public EntProtoId FirebreathAction = "ActionFireDragonFirebreath";

    /// <summary>
    /// Whether to grant Wing Dash. Opt-in while its movement still needs work.
    /// </summary>
    [DataField] public bool WingDashEnabled;

    /// <summary>
    /// Sound played for a successful ability, including for the controlling player.
    /// </summary>
    [DataField] public SoundSpecifier? AbilitySound;

    /// <summary>
    /// Projectile prototype launched by Ancient Flame.
    /// </summary>
    [DataField] public EntProtoId Projectile = "ProjectileAncientFlame";

    /// <summary>
    /// Ground fire prototype spawned by Tail Slam and Firebreath.
    /// </summary>
    [DataField] public EntProtoId GroundFirePrototype = "FireDragonGroundFire";

    /// <summary>
    /// Emote that triggers the Roar action and shares its cooldown.
    /// </summary>
    [DataField] public ProtoId<EmotePrototype> RoarEmote = "FireDragonRoar";

    /// <summary>
    /// Maximum targeting range in meters for Ancient Flame and Firebreath.
    /// </summary>
    [DataField] public float AbilityRange = 12f;

    /// <summary>
    /// Ancient Flame projectile speed in meters per second.
    /// </summary>
    [DataField] public float ProjectileSpeed = 7f;

    /// <summary>
    /// Wing Dash movement speed in meters per second.
    /// </summary>
    [DataField] public float DashSpeed = 10f;

    /// <summary>
    /// Duration of Wing Dash.
    /// </summary>
    [DataField] public TimeSpan DashDuration = TimeSpan.FromSeconds(0.45);

    /// <summary>
    /// Width of the firebreath lane in tiles.
    /// </summary>
    [DataField] public int BreathWidth = 3;

    /// <summary>
    /// Length of the firebreath lane in tiles.
    /// </summary>
    [DataField] public int BreathLength = 4;

    /// <summary>
    /// Time for firebreath to advance by one full row.
    /// </summary>
    [DataField] public TimeSpan BreathRowInterval = TimeSpan.FromSeconds(0.25);

    /// <summary>
    /// Distance in tiles between firebreath advancement updates.
    /// </summary>
    [DataField] public float BreathStepDistance = 0.25f;

    /// <summary>
    /// Delay before a newly created fire wave starts advancing.
    /// </summary>
    [DataField] public TimeSpan WaveStartDelay = TimeSpan.FromSeconds(0.15);

    /// <summary>
    /// Tail Slam radius in tiles, clamped to one or two by the ability.
    /// </summary>
    [DataField] public int SlamRadius = 2;

    /// <summary>
    /// Time between successive Tail Slam fire rings.
    /// </summary>
    [DataField] public TimeSpan SlamRingInterval = TimeSpan.FromSeconds(0.525);

    /// <summary>
    /// Lifetime of the ground flames created by Tail Slam.
    /// </summary>
    [DataField] public TimeSpan SlamFireLifetime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Minimum time between NPC ability decisions.
    /// </summary>
    [DataField] public TimeSpan NpcDecisionInterval = TimeSpan.FromSeconds(0.25);

    /// <summary>
    /// Maximum target distance in meters for NPCs to select Firebreath.
    /// </summary>
    [DataField] public float NpcBreathRange = 4.5f;

    /// <summary>
    /// Minimum target distance in meters for NPCs to consider Wing Dash.
    /// </summary>
    [DataField] public float NpcDashMinRange = 2f;

    /// <summary>
    /// Next time the NPC can make an ability decision.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextNpcAction;
}
