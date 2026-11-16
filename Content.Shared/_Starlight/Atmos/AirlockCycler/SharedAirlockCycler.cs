using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Atmos.AirlockCycler;

[Serializable, NetSerializable]
public enum AirlockCyclerUiKey : byte
{
    Key,
}

/// <summary>
/// Current cycler state like disabled, closing.
/// </summary>
[Serializable, NetSerializable]
public enum AirlockCyclerState : byte
{
    /// <summary>No power, unbolted doors.</summary>
    Off,

    /// <summary>No work! Time to drink beer</summary>
    Idle,

    /// <summary>This lazy door closes so long, so I need to wait!</summary>
    Closing,

    /// <summary>Pumping gas from the tank into the chamber. It's magic!</summary>
    Pressurizing,

    /// <summary>Pumping gas from the chamber into the tank. It's not a magic =(</summary>
    Depressurizing,
}

/// <summary>
/// Describes issue when cycler can't do smth.
/// </summary>
[Serializable, NetSerializable]
public enum AirlockCyclerProblem : byte
{
    None,

    /// <summary>No vent is linked! I can't pull or push Gas!</summary>
    NoVent,

    /// <summary>No gas? No Power? Full Tank? Whatever, just drink beer!</summary>
    Stalled,

    /// <summary>Someone blocking your door with object or it's unpowered, today you'll stay outdoor!</summary>
    DoorObstructed,

    /// <summary>
    /// The doors of the target side lead to different pressures (say vacuum and a room). No single chamber pressure
    /// is safe for all of them, so the doors stay shut until someone forces them.
    /// </summary>
    IncompatiblePressure,

    /// <summary>
    /// A vent is linked but the chamber or the space behind a target door can not be measured (the vent can not
    /// reach the door, or only walls lie behind it). The doors stay shut until someone forces them.
    /// </summary>
    NoReading,

    /// <summary>
    /// A closed door that has to stay shut can not be bolted (it has no power), so anyone could open it by hand
    /// while the other side opens. The cycle waits, even when forced.
    /// </summary>
    DoorUnsecured,

    /// <summary>The chamber has to be filled but no inlet vent is linked. The doors stay shut until forced.</summary>
    NoInlet,

    /// <summary>The chamber has to be emptied but no outlet is linked. The doors stay shut until forced.</summary>
    NoOutlet,
}

[Serializable, NetSerializable]
public enum AirlockCyclerVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum AirlockCyclerButtonVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum AirlockCyclerButtonState : byte
{
    /// <summary>No power, not linked, I'm sad =(</summary>
    Off,

    /// <summary>No work, just chilling.</summary>
    Standby,

    /// <summary>My big brother doing work, I'm warning you!</summary>
    Cycling,
}

[Serializable, NetSerializable]
public sealed class AirlockCyclerSideData
{
    /// <summary>
    /// Index of the side in the controller's side list.
    /// </summary>
    public int Index;

    /// <summary>
    /// Localization id of the side name.
    /// </summary>
    public string Name = string.Empty;

    /// <summary>
    /// Average pressure behind the doors of this side, in kPa. Null when it could not be measured.
    /// </summary>
    public float? OutsidePressure;

    /// <summary>
    /// Amount of doors, added for UI.
    /// </summary>
    public int Doors;

    /// <summary>
    /// Amount of open doors, added for UI.
    /// </summary>
    public int OpenDoors;
}

[Serializable, NetSerializable]
public sealed class AirlockCyclerBoundUserInterfaceState : BoundUserInterfaceState
{
    /// <summary>
    /// Current cycler state.
    /// </summary>
    public AirlockCyclerState State;

    /// <summary>
    /// Current cycler problem.
    /// </summary>
    public AirlockCyclerProblem Problem;

    /// <summary>
    /// No vent? =(
    /// </summary>
    public bool NoVent;

    /// <summary>
    /// Every door is bolted until someone asks for a cycle.
    /// </summary>
    public bool Lockdown;

    /// <summary>
    /// Side the controller is cycling towards, or the side that was opened last when idle.
    /// </summary>
    public int? TargetSide;

    /// <summary>
    /// Are we drinking beer?
    /// </summary>
    public bool Idle => State == AirlockCyclerState.Idle;

    /// <summary>
    /// Current chamber pressure.
    /// </summary>
    public float? ChamberPressure;

    /// <summary>
    /// Pressure the chamber is pumped to, in kPa.
    /// </summary>
    public float? TargetPressure;

    /// <summary>
    /// Pressure in the pipe net of the first vent (the buffer tank), in kPa.
    /// </summary>
    public float? TankPressure;

    public List<AirlockCyclerSideData> Sides = new();

    /// <summary>
    /// Sides waiting for their cycle, next first.
    /// </summary>
    public List<int> Queued = new();
}

/// <summary>
/// Cycle the chamber towards a side and open its doors.
/// </summary>
[Serializable, NetSerializable]
public sealed class AirlockCyclerCycleMessage(int side) : BoundUserInterfaceMessage
{
    public readonly int Side = side;
}

/// <summary>
/// Open the doors of the target side right away, without waiting for the pressure to match.
/// </summary>
[Serializable, NetSerializable]
public sealed class AirlockCyclerForceMessage : BoundUserInterfaceMessage;

/// <summary>
/// Stop any cycle and bolt every door shut until someone asks for a cycle again.
/// </summary>
[Serializable, NetSerializable]
public sealed class AirlockCyclerLockdownMessage : BoundUserInterfaceMessage;
