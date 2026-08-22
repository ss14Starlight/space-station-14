using Content.Shared.Destructible.Thresholds;
using Content.Shared.EntityTable.EntitySelectors;


namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(BasicStationEventSchedulerSystem))]
public sealed partial class BasicStationEventSchedulerComponent : Component
{
    /// <summary>
    /// How long the the scheduler waits to begin starting rules.
    /// </summary>
    [DataField]
    public float MinimumTimeUntilFirstEvent = 200;

    /// <summary>
    /// The minimum and maximum time between rule starts in seconds.
    /// </summary>
    [DataField]
    public MinMax MinMaxEventTiming = new(3 * 60, 10 * 60);

    /// <summary>
    /// How long until the next check for an event runs, is initially set based on MinimumTimeUntilFirstEvent & MinMaxEventTiming.
    /// </summary>
    [DataField]
    public float TimeUntilNextEvent;

    /// <summary>
    /// The gamerules that the scheduler can choose from
    /// </summary>
    /// Reminder that though we could do all selection via the EntityTableSelector, we also need to consider various <see cref="StationEventComponent"/> restrictions.
    /// As such, we want to pass a list of acceptable game rules, which are then parsed for restrictions by the <see cref="EventManagerSystem"/>.
    [DataField(required: true)]
    public EntityTableSelector ScheduledGameRules = default!;
    // Starlight-start

    /// <summary>
    /// How many automatically selected future events we keep visible in the queue.
    /// </summary>
    [DataField]
    public int AutoQueueLookahead = 2;

    /// <summary>
    /// The current event queue managed by the scheduler actor.
    /// </summary>
    public readonly List<QueuedStationEventEntry> EventQueue = new();

    /// <summary>
    /// When this scheduler was paused because station events were switched off, if it is.
    /// </summary>
    /// <remarks>
    /// Queue entries carry absolute trigger times, so a scheduler left disabled for ten
    /// minutes came back with every entry overdue and fired them all in a single tick. The
    /// queue is shifted forward by the paused interval when events are turned back on.
    /// </remarks>
    public TimeSpan? PausedAt;
}

/// <summary>
/// Represents a scheduled or manually queued station event waiting to be triggered.
/// </summary>
public sealed class QueuedStationEventEntry
{
    /// <summary>
    /// Unique identifier for this queued entry across all active schedulers.
    /// </summary>
    public int Id;

    /// <summary>
    /// Prototype ID of the station event.
    /// </summary>
    public string EventId = string.Empty;

    /// <summary>
    /// Timestamp when this entry was added to the queue.
    /// </summary>
    public TimeSpan QueuedAt;

    /// <summary>
    /// Scheduled timestamp when this event should trigger.
    /// </summary>
    public TimeSpan TriggerTime;

    /// <summary>
    /// True if automatically planned by the scheduler; false if manually added by an admin.
    /// </summary>
    public bool Automatic;
    // Starlight-end
}
