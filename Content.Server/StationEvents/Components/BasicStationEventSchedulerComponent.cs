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
    /// Runtime identifier used by the admin panel to mutate queued entries.
    /// </summary>
    public int NextQueueId = 1;
}

public sealed class QueuedStationEventEntry
{
    public int Id;
    public string EventId = string.Empty;
    public TimeSpan QueuedAt;
    public TimeSpan TriggerTime;
    public bool Automatic;
}
