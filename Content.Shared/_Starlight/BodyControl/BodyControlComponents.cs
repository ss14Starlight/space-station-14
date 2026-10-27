using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.BodyControl;

/// <summary>
/// Describes which operations a controller may perform on a body.
/// Providers authorize the grant; command handlers enforce its permissions and optional allowlists.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class BodyControlAccess
{
    /// <summary>
    /// Permission identifiers understood by the built-in movement and interaction handlers.
    /// </summary>
    public static readonly string[] StandardPermissions =
        ["body.move", "body.hands", "body.interact", "body.inventory", "body.speak", "body.actions"];
    /// <summary>
    /// Bypasses permission and hand, body-part and action allowlists.
    /// Ordinary gameplay checks and action ownership checks still apply.
    /// </summary>
    [DataField] public bool FullControl;
    /// <summary>
    /// Granted operation identifiers. An empty set grants no operations unless full control is enabled.
    /// </summary>
    [DataField] public HashSet<string> Permissions = new();
    /// <summary>
    /// Allowed hand IDs. Null imposes no hand-ID restriction; an empty set permits no hands.
    /// The body.hands permission and body-part restrictions still apply.
    /// </summary>
    [DataField] public HashSet<string>? Hands;
    /// <summary>
    /// Body-part subtrees whose hands may be used, in addition to the hand-ID allowlist.
    /// Null imposes no body-part restriction; an empty set permits no hands.
    /// </summary>
    [DataField] public HashSet<NetEntity>? Parts;
    /// <summary>
    /// Allowed action prototype IDs. Null imposes no prototype restriction; an empty set permits none.
    /// Combat-mode toggling uses body.interact rather than this action allowlist.
    /// </summary>
    [DataField] public HashSet<EntProtoId>? Actions;

    public bool Allows(string permission) => FullControl || Permissions.Contains(permission);

    /// <summary>
    /// Compares grant values without depending on collection order or object identity.
    /// Null and empty allowlists remain distinct because they grant different access.
    /// </summary>
    public bool SameAs(BodyControlAccess other) => FullControl == other.FullControl
        && Permissions.SetEquals(other.Permissions) && Same(Hands, other.Hands)
        && Same(Parts, other.Parts) && Same(Actions, other.Actions);

    private static bool Same<T>(HashSet<T>? first, HashSet<T>? second)
        => first == null ? second == null : second != null && first.SetEquals(second);

    /// <summary>
    /// Copies the grant and its mutable collections so later edits do not change an active grant implicitly.
    /// </summary>
    public BodyControlAccess Copy() => new()
    {
        FullControl = FullControl,
        Permissions = new(Permissions),
        Hands = Hands == null ? null : new(Hands),
        Parts = Parts == null ? null : new(Parts),
        Actions = Actions == null ? null : new(Actions),
    };
}

/// <summary>
/// Stores a control grant on the player's actual attached entity.
/// Controlling another body does not transfer the player's mind or session to that body.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BodyControlSourceComponent : Component
{
    /// <summary>
    /// Physical body receiving commands. It may be the controller's own entity when sharing its native body.
    /// </summary>
    [AutoNetworkedField] public EntityUid Body;
    [AutoNetworkedField] public BodyControlAccess Access = new();
    /// <summary>
    /// This controller's selected hand, independent of the body's shared active-hand field.
    /// </summary>
    [AutoNetworkedField] public string? SelectedHand;
    /// <summary>
    /// Provider owning an exclusive grant. Its deletion invalidates that grant.
    /// </summary>
    [AutoNetworkedField] public EntityUid? Provider;
    /// <summary>
    /// The provider supplies movement and presentation; this system only reserves exclusive ownership.
    /// </summary>
    [AutoNetworkedField] public bool ExternallyDriven;

    /// <summary>
    /// Mind present when cooperative control began, used to reject a grant after mind replacement.
    /// </summary>
    public EntityUid Mind;
    public MoveButtons Held;
    public TimeSpan NextDeniedPopup;
    /// <summary>
    /// Server-wide press sequence numbers for held directions, used to resolve conflicting movement input.
    /// </summary>
    public readonly Dictionary<MoveButtons, ulong> PressOrder = new();

    public override bool SendOnlyToOwner => true;
}

/// <summary>
/// Tracks the controllers of one physical body and the state needed to restore its normal movement and AI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BodyControlTargetComponent : Component
{
    /// <summary>
    /// Number of registered controllers. More than one disables local movement prediction for the body.
    /// </summary>
    [AutoNetworkedField] public int ControllerCount;
    /// <summary>
    /// The only registered controller, or null when the count is not exactly one.
    /// </summary>
    [AutoNetworkedField] public EntityUid? SoleController;
    [AutoNetworkedField] public bool ExternallyDriven;

    public readonly HashSet<EntityUid> Controllers = new();
    /// <summary>
    /// Whether NPC AI was awake before control paused it; null means no previous AI state was captured.
    /// </summary>
    public bool? RestoreNpcAwake;
}

/// <summary>
/// Raised on the input source before the normal mover handles a directional or walk-key change.
/// Setting Handled skips local mover processing; it does not suppress transmission of the input to the server.
/// </summary>
[ByRefEvent]
public struct BodyControlMoveInputEvent(MoveButtons button, ushort subTick, bool pressed)
{
    public readonly MoveButtons Button = button;
    public readonly ushort SubTick = subTick;
    public readonly bool Pressed = pressed;
    public bool Handled;
}

/// <summary>
/// Raised on an exclusive provider after its grant is revoked by external cleanup.
/// The provider must release its remaining movement and presentation state.
/// </summary>
[ByRefEvent]
public readonly record struct BodyControlProviderReleasedEvent(EntityUid Actor, EntityUid Body);
