using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Actions.Components;

/// <summary>
/// Struggle minigame state for a latch target: a cursor sweeps back and forth
/// across a bar, and pressing Struggle while it's in the sweet spot shortens
/// the latch. Kept separate from <see cref="LatchedComponent"/> because this
/// updates often and LatchedComponent's state handler does one-time setup.
/// </summary>
/// <remarks>
/// The cursor is never networked per-frame. Both sides derive it from
/// <see cref="SegmentStart"/>, <see cref="SegmentPosition"/> and
/// <see cref="Speed"/>; client input is tick-stamped and the server processes
/// it at that same tick, so both see the same cursor position.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LatchStruggleComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// Centre of the current attempt's sweet spot, 0 to 1 along the bar.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float ZoneCenter;

    /// <summary>
    /// When the cursor starts (or resumes) moving from <see cref="SegmentPosition"/>.
    /// Before this, the cursor holds still. <see cref="TimeSpan.MaxValue"/> while paused.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan SegmentStart;

    /// <summary>
    /// Cursor position at <see cref="SegmentStart"/>, unfolded over 0 to 2:
    /// 0 to 1 is travelling right, 1 to 2 is travelling back left.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float SegmentPosition;

    /// <summary>
    /// Cursor speed, in bar lengths per second.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float Speed;

    /// <summary>
    /// Why struggling is currently unavailable, if it is.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public LatchStruggleBlock Block;

    /// <summary>
    /// Grade of the most recent press, shown briefly before the next attempt.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public LatchStruggleResult LastResult;

    [ViewVariables, AutoNetworkedField]
    public float LastPressPosition;

    [ViewVariables, AutoNetworkedField]
    public float LastZoneCenter;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan LastPressTime;

    /// <summary>
    /// Bite Harder speeds the cursor up until this time. Server-only.
    /// </summary>
    [ViewVariables]
    public TimeSpan FrenzyEndTime;
}

[Serializable, NetSerializable]
public enum LatchStruggleResult : byte
{
    None,
    Miss,
    Good,
    Perfect,
}

[Serializable, NetSerializable]
public enum LatchStruggleBlock : byte
{
    None,
    Exhausted,
    Stunned,
    Incapacitated,
}
