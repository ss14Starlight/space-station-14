using Content.Shared._Starlight.Atmos.AirlockCycler;
using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Atmos.AirlockCycler;

[RegisterComponent, Access(typeof(AirlockCyclerSystem))]
[AutoGenerateComponentPause]
public sealed partial class AirlockCyclerComponent : Component
{
    /// <summary>
    /// One source port per side, in the order the console lists them.
    /// </summary>
    [DataField]
    public List<ProtoId<SourcePortPrototype>> Sides = new()
    {
        "AirlockCyclerSideA",
        "AirlockCyclerSideB",
        "AirlockCyclerSideC",
        "AirlockCyclerSideD",
    };

    /// <summary>
    /// Air vents that fill the chamber.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> InletPort = "AirlockCyclerInlets";

    /// <summary>
    /// Vents, scrubbers or pumps that empty the chamber.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> OutletPort = "AirlockCyclerOutlets";

    /// <summary>
    /// Once the chamber is this close to the pressure behind the doors (kPa), the controller keeps pumping for at most
    /// <see cref="SettleTime"/> to finish the job, then opens.
    /// </summary>
    [DataField]
    public float PressureTolerance = 10f;

    /// <summary>
    /// The doors open right away once the chamber is this close to the pressure behind them, in kPa.
    /// </summary>
    [DataField]
    public float MatchedPressure = 1f;

    /// <summary>
    /// The doors of one side may lead to pressures this far apart (kPa) and still be cycled together. Beyond that no
    /// chamber pressure is safe for all of them, and the controller refuses until forced.
    /// </summary>
    [DataField]
    public float IncompatiblePressure = 10f;

    /// <summary>
    /// Determines how much time we should wait once after we reached <see cref="PressureTolerance"/> before opening doors.
    /// Was added since I noticed that even with tolerance system since chamber has multiple tiles you can like... throw ~100kPa to space.
    /// </summary>
    [DataField]
    public TimeSpan SettleTime = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Just flex variable used to save time when we should open door.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan? SettleUntil;

    /// <summary>
    /// If the chamber pressure changes by less than <see cref="StallPressure"/> during this time, the cycle is stalled.
    /// </summary>
    [DataField]
    public TimeSpan StallTime = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Determines percentage of target pressure (from 0% to 100% in float), if current pressure less/bigger then this percentage, we deciding that airlock stalled.
    /// </summary>
    [DataField]
    public float StallPressure = 0.5f;

    /// <summary>
    /// The chamber flood fill stops after this many tiles (a door left open towards a hallway or space).
    /// </summary>
    [DataField]
    public int MaxChamberTiles = 64;

    /// <summary>
    /// How often we should update cycler state.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Just flex variable used to save time when we should update cycler state.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Current cycler state. Nothing special.
    /// </summary>
    [ViewVariables, DataField]
    public AirlockCyclerState State = AirlockCyclerState.Off;

    /// <summary>
    /// Current cycler problem. Nothing special.
    /// </summary>
    [ViewVariables]
    public AirlockCyclerProblem Problem = AirlockCyclerProblem.None;

    /// <summary>
    /// The side being cycled to, or the side opened last while idle.
    /// </summary>
    [ViewVariables, DataField]
    public int? TargetSide;

    /// <summary>
    /// Open the target side without waiting for the pressure (asked for from the console, or by pressing the
    /// button of a stalled side again).
    /// </summary>
    [ViewVariables, DataField]
    public bool Force;

    /// <summary>
    /// Who asked for the current cycle. The target side's doors are opened with their access, so a door only swings
    /// open for someone who could open it by hand; for everyone else it is just unbolted.
    /// </summary>
    [ViewVariables, DataField]
    public EntityUid? Requester;

    /// <summary>
    /// Power failed mid-cycle. The doors were let go so people can pass by hand; once power is back the cycle picks
    /// up again and the target side is shut and bolted until it completes.
    /// </summary>
    [ViewVariables, DataField]
    public bool Interrupted;

    /// <summary>
    /// Every door is bolted shut until someone asks for a cycle.
    /// </summary>
    [ViewVariables, DataField]
    public bool Lockdown;

    /// <summary>
    /// Requests that came in while a cycle was running, oldest first. Like a lift: the running cycle finishes, the
    /// doors stay open for <see cref="QueueDelay"/>, then the next request starts.
    /// </summary>
    [ViewVariables, DataField]
    public List<AirlockCyclerRequest> Queue = new();

    /// <summary>
    /// How long the opened side stays open before the next queued cycle starts closing it.
    /// </summary>
    [DataField]
    public TimeSpan QueueDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// The next queued cycle may start from this time on.
    /// </summary>
    [ViewVariables, DataField, AutoPausedField]
    public TimeSpan QueueReadyAt;

    /// <summary>
    /// Set on the first update after spawn or load. A saved grid brings back the vents still enabled with the cycle's
    /// settings while the cycle itself has to be picked up again, so they are stopped once the links are back.
    /// </summary>
    [ViewVariables]
    public bool Reconciled;

    /// <summary>
    /// Devices whose link went away. Released on the next update once no device link is left: a bulk unlink raises
    /// the disconnect for every port before any link is gone, so the decision can not be made in the event.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> PendingRelease = new();

    /// <summary>
    /// Last time when we get chamber pressuring/depressuring progress.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastProgress;

    /// <summary>
    /// Last pressure when we get chamber pressuring/depressuring progress.
    /// </summary>
    [ViewVariables]
    public float LastProgressPressure;
}

/// <summary>
/// A queued cycle request: the side to cycle to and who asked, whose access opens the doors.
/// </summary>
[DataDefinition]
public sealed partial class AirlockCyclerRequest
{
    [DataField]
    public int Side;

    [DataField]
    public EntityUid? Requester;
}
