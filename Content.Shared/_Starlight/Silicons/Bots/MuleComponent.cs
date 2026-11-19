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
    public bool On = true;

    /// <summary>
    /// What it's doing right now.
    /// </summary>
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

    /// <summary>
    /// The radio channel it reports on.
    /// </summary>
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
    public float ChargePerTile = 0.216f;

    /// <summary>
    /// Which of its wires are cut.
    /// </summary>
    [DataField, AutoNetworkedField]
    public MuleWires CutWires;

    /// <summary>
    /// Speed with only the first motor wire cut.
    /// </summary>
    [DataField]
    public float FastMotor = 1.8f;

    /// <summary>
    /// Speed with only the second motor wire cut.
    /// </summary>
    [DataField]
    public float MediumMotor = 1.3f;

    /// <summary>
    /// Until when it's creeping along because someone is lying in its way.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? CarefulUntil;

    /// <summary>
    /// How long it keeps creeping after it last saw someone lying in its way.
    /// </summary>
    [DataField]
    public TimeSpan CarefulTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Extra time it takes per tile while driving carefully, as a share of its normal time per tile.
    /// </summary>
    [DataField]
    public float CarefulDelay = 2f / 3f;

    /// <summary>
    /// How long someone it bumps into stays down, with the safety wire cut.
    /// </summary>
    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Blunt damage for running someone over, before <see cref="RunOverMultiplier"/>.
    /// </summary>
    [DataField]
    public float RunOverMin = 5f;

    /// <summary>
    /// The most blunt damage for running someone over, before <see cref="RunOverMultiplier"/>.
    /// </summary>
    [DataField]
    public float RunOverMax = 15f;

    /// <summary>
    /// What the rolled run-over damage is multiplied by.
    /// </summary>
    [DataField]
    public float RunOverMultiplier = 6f;

    /// <summary>
    /// Blood spilled by whoever it runs over.
    /// </summary>
    [DataField]
    public float RunOverBlood = 5f;

    /// <summary>
    /// Played when it runs someone over.
    /// </summary>
    [DataField]
    public SoundSpecifier RunOverSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    /// <summary>
    /// Played when it gets emagged.
    /// </summary>
    [DataField]
    public SoundSpecifier EmagSound = new SoundCollectionSpecifier("sparks");

    /// <summary>
    /// Played when it's stuck, or offered something it won't carry.
    /// </summary>
    [DataField]
    public SoundSpecifier SighSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    /// <summary>
    /// Played when it gives up on a route and looks for another way.
    /// </summary>
    [DataField]
    public SoundSpecifier AnnoyedSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    /// <summary>
    /// Played when it arrives with a delivery, if it reports.
    /// </summary>
    [DataField]
    public SoundSpecifier ArrivedSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");

    /// <summary>
    /// Played when something is loaded or unloaded.
    /// </summary>
    [DataField]
    public SoundSpecifier LoadSound = new SoundCollectionSpecifier("MuleLoad");

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

    /// <summary>
    /// Whether the current route stops at a door short of the beacon.
    /// </summary>
    [ViewVariables]
    public bool StopsShort;

    /// <summary>
    /// When it next looks at doors, people in the way and its progress.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextCheck;

    /// <summary>
    /// Who it has knocked down or run over lately, so it hits each once per pass.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> RecentHits = new();

    /// <summary>
    /// Loads it left under itself, which it drives through until it's clear of each.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> DroppedUnder = new();

    /// <summary>
    /// The tile it last checked for people on; it only knocks over or runs over people on tiles it drives onto.
    /// </summary>
    [ViewVariables]
    public Vector2i? HazardTile;

    /// <summary>
    /// When it may next look for a route after failing to find one.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextRouteTry;

    /// <summary>
    /// When its light stops flashing.
    /// </summary>
    [ViewVariables]
    public TimeSpan? FlashUntil;

    /// <summary>
    /// Where it was at the last check, for working out the distance driven.
    /// </summary>
    [ViewVariables]
    public Vector2 LastPosition;

    /// <summary>
    /// Distance driven that hasn't been paid for in charge yet.
    /// </summary>
    [ViewVariables]
    public float Travelled;

    /// <summary>
    /// Where it was at the last stuck check.
    /// </summary>
    [ViewVariables]
    public Vector2 StuckPosition;

    /// <summary>
    /// When the last stuck check was.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastMoved;

    /// <summary>
    /// How many stuck checks this run it hasn't got anywhere.
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
}

[Serializable, NetSerializable]
public enum MuleVisuals : byte
{
    Flash,
    Emagged,
}

[Flags, Serializable, NetSerializable]
public enum MuleWires : byte
{
    None = 0,
    Power1 = 1 << 0,
    Power2 = 1 << 1,
    Avoidance = 1 << 2,
    LoadCheck = 1 << 3,
    Motor1 = 1 << 4,
    Motor2 = 1 << 5,
    Beacon = 1 << 6,
}
