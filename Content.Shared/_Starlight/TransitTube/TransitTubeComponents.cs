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
    /// Draw a metal ring on every few joints so long straight runs don't look empty.
    /// </summary>
    [DataField]
    public bool Rings;
}

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
    [ViewVariables]
    public bool StopRequested;

    /// <summary>
    /// The name passengers pick this station by. If unset, the station's label (hand labeler) is used;
    /// stations with neither are not offered as destinations.
    /// </summary>
    [DataField]
    public string? Destination;

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

    [DataField]
    public TimeSpan OpenTime = TimeSpan.FromSeconds(0.5);

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

[RegisterComponent, AutoGenerateComponentPause]
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
    /// Exit cooldown.
    /// </summary>
    [DataField]
    public TimeSpan ExitAttemptCooldown = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Last exit attempt time.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan LastExitAttempt;

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
    [DataField]
    public float Temperature = 1500f;
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
}

[Serializable, NetSerializable]
public sealed partial class TransitPodBoardDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum TransitStationUiKey : byte
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
