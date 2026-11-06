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
    /// Determines port for vent.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> VentPort = "AirlockCyclerVents";

    /// <summary>
    /// Once the chamber is this close to the pressure behind the doors (kPa), the controller keeps pumping for at most
    /// <see cref="SettleTime"/> to finish the job, then opens.
    /// </summary>
    [DataField]
    public float PressureTolerance = 2f;

    /// <summary>
    /// The doors open right away once the chamber is this close to the pressure behind them, in kPa.
    /// </summary>
    [DataField]
    public float MatchedPressure = 0.2f;

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
    [ViewVariables]
    public AirlockCyclerState State = AirlockCyclerState.Off;

    /// <summary>
    /// Current cycler problem. Nothing special.
    /// </summary>
    [ViewVariables]
    public AirlockCyclerProblem Problem = AirlockCyclerProblem.None;

    /// <summary>
    /// The side being cycled to, or the side opened last while idle.
    /// </summary>
    [ViewVariables]
    public int? TargetSide;

    /// <summary>
    /// Open the target side without waiting for the pressure (asked for from the console, or by pressing the
    /// button of a stalled side again).
    /// </summary>
    [ViewVariables]
    public bool Force;

    /// <summary>
    /// Who asked for the current cycle. The target side's doors are opened with their access, so a door only swings
    /// open for someone who could open it by hand; for everyone else it is just unbolted.
    /// </summary>
    [ViewVariables]
    public EntityUid? Requester;

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
