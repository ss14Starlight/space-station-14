using System.Numerics;
using Content.Shared.Access;
using Content.Shared.Radio;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Silicons.Bots;

/// <summary>
/// A cargo robot that carries one crate at a time to a station beacon and drives back home.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MuleComponent : Component
{
    /// <summary>
    /// The container the load sits in.
    /// </summary>
    [DataField]
    public string ContainerId = "mule_load";

    /// <summary>
    /// What it agrees to carry.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Whether it's switched on.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool On;

    [DataField, AutoNetworkedField]
    public MuleMode Mode = MuleMode.Idle;

    /// <summary>
    /// The beacon it's sent to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Destination;

    /// <summary>
    /// The beacon it goes back to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Home;

    /// <summary>
    /// Go home after a delivery.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoReturn = true;

    /// <summary>
    /// Pick up a crate where it arrives when it has nothing to unload.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoPickup = true;

    /// <summary>
    /// Announce loads and deliveries on the radio.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Report = true;

    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Supply";

    /// <summary>
    /// Extra access it may use on the first set of doors into a department, so it can reach a beacon in the lobby.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AccessLevelPrototype>> EntranceAccess = new();

    /// <summary>
    /// Charge used for every tile driven.
    /// </summary>
    [DataField]
    public float ChargePerTile = 0.2f;

    [DataField]
    public SoundSpecifier SighSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    [DataField]
    public SoundSpecifier AnnoyedSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    [DataField]
    public SoundSpecifier ArrivedSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");

    [DataField]
    public SoundSpecifier LoadSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    /// <summary>
    /// Doors on the current route it may open with <see cref="EntranceAccess"/>.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> EntranceDoors = new();

    /// <summary>
    /// Points it still has to steer through on the current route, the last being where it stops.
    /// </summary>
    [ViewVariables]
    public List<EntityCoordinates> Waypoints = new();

    [ViewVariables]
    public TimeSpan NextCheck;

    /// <summary>
    /// Where it was when it last moved a whole tile, and when.
    /// </summary>
    [ViewVariables]
    public Vector2 LastPosition;

    [ViewVariables]
    public TimeSpan LastMoved;

    /// <summary>
    /// How many checks in a row it hasn't moved.
    /// </summary>
    [ViewVariables]
    public int Stuck;
}

[Serializable, NetSerializable]
public enum MuleMode : byte
{
    Idle,
    Delivering,
    GoingHome,
    Blocked,
    NoRoute,
}

[Serializable, NetSerializable]
public enum MuleVisuals : byte
{
    On,
}
