using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.BodyControl;

/// <summary>Permissions granted by the system that owns a body.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class BodyControlAccess
{
    [DataField] public bool FullControl;
    [DataField] public HashSet<string> Permissions = new();
    [DataField] public HashSet<string>? Hands;
    [DataField] public HashSet<NetEntity>? Parts;
    [DataField] public HashSet<EntProtoId>? Actions;

    public bool Allows(string permission) => FullControl || Permissions.Contains(permission);

    public BodyControlAccess Copy() => new()
    {
        FullControl = FullControl,
        Permissions = new(Permissions),
        Hands = Hands == null ? null : new(Hands),
        Parts = Parts == null ? null : new(Parts),
        Actions = Actions == null ? null : new(Actions),
    };
}

/// <summary>A control grant on the actual player entity, without moving its mind or session.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BodyControlSourceComponent : Component
{
    [AutoNetworkedField] public EntityUid Body;
    [AutoNetworkedField] public BodyControlAccess Access = new();
    [AutoNetworkedField] public string? SelectedHand;
    [AutoNetworkedField] public EntityUid? Provider;
    [AutoNetworkedField] public bool ExternallyDriven;

    public EntityUid Mind;
    public MoveButtons Held;
    public readonly Dictionary<MoveButtons, ulong> PressOrder = new();

    public override bool SendOnlyToOwner => true;
}

/// <summary>The players authorized to operate one physical body.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BodyControlTargetComponent : Component
{
    [AutoNetworkedField] public int ControllerCount;
    [AutoNetworkedField] public EntityUid? SoleController;
    [AutoNetworkedField] public bool ExternallyDriven;

    public readonly HashSet<EntityUid> Controllers = new();
    public bool? RestoreNpcAwake;
}

/// <summary>Allows content to handle movement before the normal mover consumes the command.</summary>
[ByRefEvent]
public struct BodyControlMoveInputEvent(MoveButtons button, ushort subTick, bool pressed)
{
    public readonly MoveButtons Button = button;
    public readonly ushort SubTick = subTick;
    public readonly bool Pressed = pressed;
    public bool Handled;
}

/// <summary>A provider's lease was revoked outside the provider's own cleanup.</summary>
[ByRefEvent]
public readonly record struct BodyControlProviderReleasedEvent(EntityUid Actor, EntityUid Body);
