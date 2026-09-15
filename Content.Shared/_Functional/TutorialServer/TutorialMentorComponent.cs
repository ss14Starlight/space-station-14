namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks a soft-following tutorial mentor tied to one participant.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialMentorComponent : Component
{
    /// <summary>
    /// Player body this mentor coaches and follows.
    /// </summary>
    [DataField]
    public EntityUid PlayerUid;

    /// <summary>
    /// True when this mentor leads instead of follows (<see cref="TutorialMentorMode.Lead"/>).
    /// Read by <c>TutorialMentorFollowSystem</c>, which must stand down for these: two systems
    /// writing FollowTarget every tick would leave the mentor pinned wherever the last writer won.
    /// </summary>
    [DataField]
    public bool Leads;

    /// <summary>
    /// Who he turns to face on a given beat while he is standing still. Any beat not listed here
    /// faces the player, which is the right answer for almost all of them: he is talking to them.
    /// The exceptions are the beats where he is talking to somebody else, and standing with his
    /// back to the person he is addressing reads as a bug.
    /// </summary>
    [DataField]
    public List<TutorialMentorFacing> Facing = new();

    /// <summary>
    /// When set, the mentor is in a catch-up grace window that ends at this time.
    /// After the deadline, pathfinding is checked before any teleport snap.
    /// </summary>
    [ViewVariables]
    public TimeSpan? CatchUpDeadline;

    /// <summary>
    /// True while an async path check for catch-up is in flight.
    /// </summary>
    [ViewVariables]
    public bool CatchUpPathCheckInFlight;

    /// <summary>
    /// Bumped when a new catch-up is requested so stale path results are ignored.
    /// </summary>
    [ViewVariables]
    public int CatchUpGeneration;
}

/// <summary>
/// One "look at that, not at the player" override, keyed by the beat it applies to.
/// </summary>
[DataDefinition]
public sealed partial class TutorialMentorFacing
{
    [DataField(required: true)]
    public string SubGoalId = string.Empty;

    /// <summary>
    /// Tag of the entity he faces. Nearest match on his own grid wins, and a beat whose target has
    /// not turned up yet falls back to the player rather than leaving him staring at a wall.
    /// </summary>
    [DataField(required: true)]
    public string Tag = string.Empty;
}
