using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TransitTube;

[RegisterComponent]
public sealed partial class TransitTubeComponent : Component
{
    /// <summary>
    /// For junctions: the exit, before rotation, that pods take when more than one exit is equally straight.
    /// </summary>
    [DataField]
    public Direction? PreferredExit;

    /// <summary>
    /// Take <see cref="PreferredExit"/> whenever the pod can, even when going straight is possible.
    /// Side branches use it so pods turn off the line instead of passing by.
    /// </summary>
    [DataField]
    public bool ForcePreferred;

    /// <summary>
    /// For one-way tubes: the direction, before rotation, pods may pass in. Pods coming the other way
    /// are turned back.
    /// </summary>
    [DataField]
    public Direction? OneWay;

    /// <summary>
    /// The sharpest turn, in degrees, that a pod can take through this segment.
    /// </summary>
    [DataField]
    public float MaxTurn = 90f;

    /// <summary>
    /// Where the tube runs: on the floor, in a trench under it, or hung over it.
    /// </summary>
    [DataField]
    public TransitTubeLevel Level = TransitTubeLevel.Floor;

    /// <summary>
    /// A tube under or over the floor holds only this many tubes away from a ramp or a support.
    /// Further out, a hung tube sags and falls, and a buried one brings down any floor laid over it.
    /// </summary>
    [DataField]
    public int SupportRange = 3;

    /// <summary>
    /// Whether this piece holds up the tubes around it. Ramps always do.
    /// </summary>
    [DataField]
    public bool Support;

    /// <summary>
    /// How long a hung tube sags with nothing holding it before it comes down.
    /// </summary>
    [DataField]
    public TimeSpan CollapseDelay = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Played when a tube falls or the floor over it gives way.
    /// </summary>
    [DataField]
    public SoundSpecifier? CollapseSound = new SoundCollectionSpecifier("MetalThud");

    /// <summary>
    /// Played by loaders and unloaders when they move cargo.
    /// </summary>
    [DataField]
    public SoundSpecifier? CargoSound = new SoundPathSpecifier("/Audio/Machines/windoor_open.ogg", AudioParams.Default.WithVolume(-8f).WithPitchScale(1.3f));
}

[Serializable, NetSerializable]
public enum TransitTubeLevel : byte
{
    Floor,
    Under,
    Over,
}

[RegisterComponent]
public sealed partial class TransitTubeRampComponent : Component;

/// <summary>
/// A transit tube segment where pods stop, open, exchange air with the room and take passengers.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TransitTubeStationComponent : Component
{
    /// <summary>
    /// The side, before rotation, that passengers board from and are let out to.
    /// </summary>
    [DataField]
    public Direction BoardingSide = Direction.South;

    /// <summary>
    /// The pod currently docked here.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? DockedPod;

    /// <summary>
    /// Request stop: pods only stop here if someone on the platform pressed the button
    /// or a passenger asked to get off. Otherwise they pass through.
    /// </summary>
    [DataField]
    public bool RequestStop;

    /// <summary>
    /// Someone on the platform asked the next pod to stop.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool StopRequested;

    /// <summary>
    /// The localized name passengers pick this station by. If unset, the station's label (hand labeler)
    /// is used as written; stations with neither are not offered as destinations.
    /// </summary>
    [DataField]
    public LocId? Destination;

    /// <summary>
    /// A depot: pods that dock here stay until they are called to a station or sent off with a signal.
    /// Nobody boards at a depot and pods looking for any free station never pick one.
    /// </summary>
    [DataField]
    public bool Depot;

    /// <summary>
    /// Set by the Hold port: the docked pod stays until the Depart port fires.
    /// </summary>
    [ViewVariables]
    public bool Holding;

    /// <summary>
    /// The pod on its way to dock here. Other pods treat the station as busy.
    /// </summary>
    [ViewVariables]
    public EntityUid? ReservedBy;

    [DataField]
    public ProtoId<SourcePortPrototype> ArrivedPort = "TransitPodArrived";

    [DataField]
    public ProtoId<SourcePortPrototype> DepartedPort = "TransitPodDeparted";

    [DataField]
    public ProtoId<SinkPortPrototype> DepartPort = "TransitDepart";

    [DataField]
    public ProtoId<SinkPortPrototype> HoldPort = "TransitHold";

    /// <summary>
    /// How long a pod stays open before it leaves, with or without passengers. Pods run on this timer
    /// rather than waiting for riders, so pods mapped at the ends of a line keep in step.
    /// </summary>
    [DataField]
    public TimeSpan DepartDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a docking pod takes to open up.
    /// </summary>
    [DataField]
    public TimeSpan OpenTime = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// How long a departing pod takes to close before it leaves.
    /// </summary>
    [DataField]
    public TimeSpan CloseTime = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Do-after to climb into a docked pod yourself.
    /// </summary>
    [DataField]
    public TimeSpan BoardDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Do-after to put someone else into a docked pod.
    /// </summary>
    [DataField]
    public TimeSpan DraggedBoardDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Sound played at tube opening.
    /// </summary>
    [DataField]
    public SoundSpecifier? OpenSound = new SoundPathSpecifier("/Audio/Machines/windoor_open.ogg", AudioParams.Default.WithVolume(-6f));

    /// <summary>
    /// Sound played at tube closing.
    /// </summary>
    [DataField]
    public SoundSpecifier? CloseSound = new SoundPathSpecifier("/Audio/Machines/windoor_open.ogg", AudioParams.Default.WithVolume(-6f).WithPitchScale(0.85f));
}

public enum TransitPodState : byte
{
    /// <summary>Travelling through the tubes.</summary>
    Moving,
    /// <summary>Stopped in a tube with nowhere to go.</summary>
    Stalled,
    Opening,
    Open,
    Closing,
}

/// <summary>
/// A transit pod: a persistent disposal holder that rides the transit tubes. Its routing state is
/// networked so the client predicts the same way through the tubes as the server.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class TransitPodComponent : Component
{
    /// <summary>
    /// Current pod state.
    /// </summary>
    [ViewVariables]
    public TransitPodState State = TransitPodState.Stalled;

    /// <summary>
    /// When state ends.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan StateEnd;

    /// <summary>
    /// When an open pod closes and leaves.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextDeparture;

    /// <summary>
    /// The station this pod is docked at.
    /// </summary>
    [ViewVariables]
    public EntityUid? Station;

    /// <summary>
    /// Set by the routing when the pod reaches a station, read when the holder stops.
    /// </summary>
    [ViewVariables]
    public EntityUid? DockTarget;

    /// <summary>
    /// The direction the pod was going when it docked; it carries on that way if it can.
    /// </summary>
    [ViewVariables]
    public Direction TravelDirection = Direction.Invalid;

    /// <summary>
    /// Set once the pod turned back at a dead end; a second dead end in a row stalls it.
    /// </summary>
    [ViewVariables]
    public bool Bounced;

    /// <summary>
    /// Set by speed-up tubes, cleared by brakes. A boosted pod does not stop at stations and hits
    /// dead ends and other pods much harder.
    /// </summary>
    [ViewVariables]
    public bool Boosted;

    /// <summary>
    /// The normal speed of the pod, the one brakes return it to. Taken from the holder at map init.
    /// </summary>
    [ViewVariables]
    public float BaseSpeed;

    /// <summary>
    /// A passenger asked to get off at the next request stop.
    /// </summary>
    [ViewVariables]
    public bool StopRequested;

    /// <summary>
    /// The block signal holding this pod until the section ahead is clear.
    /// </summary>
    [ViewVariables]
    public EntityUid? HeldAtSignal;

    /// <summary>
    /// The block signal whose section this pod has claimed on its way past it.
    /// </summary>
    [ViewVariables]
    public EntityUid? ClaimedSignal;

    /// <summary>
    /// The tubes of the section claimed past <see cref="ClaimedSignal"/>. Other pods count the whole
    /// section as taken until this one leaves it, so two pods let in at once can't both enter.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> ClaimedSection = [];

    /// <summary>
    /// When a held pod next checks whether the section ahead is clear.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextSignalCheck;

    /// <summary>
    /// A busy station the pod waits in front of, one tube before it.
    /// </summary>
    [ViewVariables]
    public EntityUid? WaitingFor;

    /// <summary>
    /// The station this pod has claimed to dock at next.
    /// </summary>
    [ViewVariables]
    public EntityUid? Reserved;

    /// <summary>
    /// The station a passenger sent this pod to. Pods with a target pass every other station.
    /// </summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>
    /// Go to the nearest free station, waiting in the station until one is free.
    /// </summary>
    [ViewVariables]
    public bool AnyFree;

    /// <summary>
    /// The way to <see cref="Target"/>: which exit to take on entering a tube going a given way.
    /// Filled by the server when the pod leaves.
    /// </summary>
    [ViewVariables]
    public Dictionary<(EntityUid Tube, Direction Travel), Direction> Plan = new();

    /// <summary>
    /// Cargo pods carry items and crates instead of people.
    /// </summary>
    [DataField]
    public bool Cargo;

    /// <summary>
    /// Max amount of passengers that can be taken by this pod.
    /// </summary>
    [DataField]
    public int MaxPassengers = 1;

    /// <summary>
    /// How fast, per second, the air inside follows the temperature outside. Glass pods conduct heat,
    /// insulated ones barely do.
    /// </summary>
    [DataField]
    public float HeatConductivity = 0.6f;

    /// <summary>
    /// Passengers catch fire once the air in the pod gets this hot.
    /// </summary>
    [DataField]
    public float IgnitionTemperature = 573f;

    /// <summary>
    /// How much stacks dealed to humanoid in pod per once.
    /// </summary>
    [DataField]
    public float IgnitionFireStacks = 1f;

    /// <summary>
    /// Do-after to climb out of a pod stuck in the middle of a tube.
    /// </summary>
    [DataField]
    public TimeSpan ClimbOutDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How often each passenger can try to get out.
    /// </summary>
    [DataField]
    public TimeSpan ExitAttemptCooldown = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// When each passenger last tried to get out.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> LastExitAttempts = new();

    /// <summary>
    /// When a passenger asked this pod to stop. The emergency brake unlocks once a stop request
    /// has gone unanswered for <see cref="BrakeAvailableAfter"/>, so it only helps those really stuck.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan StopRequestedAt;

    /// <summary>
    /// How long a stop request must go unanswered before the emergency brake can be pulled.
    /// </summary>
    [DataField]
    public TimeSpan BrakeAvailableAfter = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Do-after to pull the emergency brake.
    /// </summary>
    [DataField]
    public TimeSpan BrakeDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The longest a braked pod stays put. It moves on earlier once nobody is left inside.
    /// </summary>
    [DataField]
    public TimeSpan BrakeDuration = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How soon after a brake ends it can be pulled again.
    /// </summary>
    [DataField]
    public TimeSpan BrakeCooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Whether the emergency brake holds this pod.
    /// </summary>
    [ViewVariables]
    public bool Braked;

    /// <summary>
    /// When the emergency brake lets go.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan BrakedUntil;

    /// <summary>
    /// When the emergency brake can next be pulled.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextBrake;

    /// <summary>
    /// A stopped pod this one waits behind, a tube short of it, rather than crashing into it.
    /// </summary>
    [ViewVariables]
    public EntityUid? WaitingBehind;

    /// <summary>
    /// Pods closer than this, in tiles, crash into each other.
    /// </summary>
    [DataField]
    public float CollisionRange = 0.9f;

    /// <summary>
    /// Damage dealt to passengers when pod bumps another pod.
    /// </summary>
    [DataField]
    public DamageSpecifier CollisionDamage = new()
    {
        DamageDict = new() { { "Blunt", 8 } },
    };

    /// <summary>
    /// How much time passengers will be stunned after collision.
    /// </summary>
    [DataField]
    public TimeSpan CollisionStun = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Collision damage and stun are multiplied by this when either pod is boosted.
    /// </summary>
    [DataField]
    public float BoostedCollisionMultiplier = 2.5f;

    /// <summary>
    /// Damage dealt to passengers when a boosted pod slams into a dead end.
    /// </summary>
    [DataField]
    public DamageSpecifier ImpactDamage = new()
    {
        DamageDict = new() { { "Blunt", 15 } },
    };

    /// <summary>
    /// Damage dealt to the dead end a boosted pod slams into. Enough hits break it open.
    /// </summary>
    [DataField]
    public DamageSpecifier ImpactTubeDamage = new()
    {
        DamageDict = new() { { "Blunt", 30 } },
    };

    /// <summary>
    /// Sound played after collision.
    /// </summary>
    [DataField]
    public SoundSpecifier? CollisionSound = new SoundCollectionSpecifier("MetalThud");

    /// <summary>
    /// Sound played after depart.
    /// </summary>
    [DataField]
    public SoundSpecifier? DepartSound = new SoundPathSpecifier("/Audio/Misc/woosh.ogg", AudioParams.Default.WithVolume(-4f));

    /// <summary>
    /// Until when this pod ignores further crashes, so one crash isn't counted every tick.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextCollision;

    /// <summary>
    /// When the pod next exchanges air or heat with its surroundings.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextAtmosUpdate;
}

/// <summary>
/// Things that heat whatever passes right above them, like lava. Used by glass transit pods.
/// </summary>
[RegisterComponent]
public sealed partial class RadiantHeatComponent : Component
{
    /// <summary>
    /// Temperature, in kelvin, that closed pods passing over this heat up towards.
    /// </summary>
    [DataField]
    public float Temperature = 1500f;
}

/// <summary>
/// One step of a pod's plan: on entering <see cref="Tube"/> going <see cref="Travel"/>, leave by <see cref="Exit"/>.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct TransitPlanStep(NetEntity Tube, Direction Travel, Direction Exit);

[Serializable, NetSerializable]
public sealed class TransitPodComponentState(
    TransitPodState state,
    bool boosted,
    bool stopRequested,
    NetEntity? target,
    bool anyFree,
    List<TransitPlanStep> plan) : ComponentState
{
    public readonly TransitPodState State = state;
    public readonly bool Boosted = boosted;
    public readonly bool StopRequested = stopRequested;
    public readonly NetEntity? Target = target;
    public readonly bool AnyFree = anyFree;
    public readonly List<TransitPlanStep> Plan = plan;
}

[Serializable, NetSerializable]
public enum TransitStationVisuals : byte
{
    State,

    /// <summary>
    /// Request stop: the call lamp is lit while someone on the platform is waiting for a pod.
    /// </summary>
    StopRequested,
}

[Serializable, NetSerializable]
public enum TransitStationVisualState : byte
{
    Closed,
    Opening,
    Open,
    Closing,
}

[Serializable, NetSerializable]
public enum TransitPodVisuals : byte
{
    Occupied,

    /// <summary>
    /// The <see cref="TransitTubeLevel"/> the pod rides on.
    /// </summary>
    Level,

    /// <summary>
    /// The pod is under a covered floor and cannot be seen.
    /// </summary>
    Hidden,

    /// <summary>
    /// How loaded the pod is, a <see cref="TransitPodLoad"/>. Cargo pods show it with lamps.
    /// </summary>
    Load,
}

[Serializable, NetSerializable]
public enum TransitPodLoad : byte
{
    Empty,

    /// <summary>
    /// Something is inside, with room for more.
    /// </summary>
    Occupied,

    /// <summary>
    /// No room left.
    /// </summary>
    Full,
}

[Serializable, NetSerializable]
public sealed partial class TransitPodBoardDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum TransitStationUiKey : byte
{
    Key,
}

/// <summary>
/// The destination picker passengers use from inside a pod. Same window and messages as a station's.
/// </summary>
[Serializable, NetSerializable]
public enum TransitPodUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed record TransitDestinationEntry(NetEntity Station, string Name);

[Serializable, NetSerializable]
public sealed class TransitStationBoundUserInterfaceState(List<TransitDestinationEntry> destinations, NetEntity? selected, bool anyFree)
    : BoundUserInterfaceState
{
    public readonly List<TransitDestinationEntry> Destinations = destinations;
    public readonly NetEntity? Selected = selected;
    public readonly bool AnyFree = anyFree;
}

/// <summary>
/// Picks where the docked pod goes next. Null with <see cref="AnyFree"/> false clears the choice.
/// </summary>
[Serializable, NetSerializable]
public sealed class TransitStationSelectDestinationMessage(NetEntity? destination, bool anyFree) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Destination = destination;
    public readonly bool AnyFree = anyFree;
}

[Serializable, NetSerializable]
public sealed partial class TransitPodClimbOutDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TransitPodBrakeDoAfterEvent : SimpleDoAfterEvent;
