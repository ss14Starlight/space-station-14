// ReSharper disable once CheckNamespace
namespace Content.Server.StationEvents.Components;

public sealed partial class BasicStationEventSchedulerComponent
{
    /// <summary>Number of future automatic events to plan.</summary>
    [DataField]
    public int AutoQueueLookahead = 2;

    public readonly List<QueuedStationEventEntry> EventQueue = new();
    public TimeSpan? PausedAt;
}

public sealed class QueuedStationEventEntry
{
    public int Id;
    public string EventId = string.Empty;
    public TimeSpan QueuedAt;
    public TimeSpan TriggerTime;
    public bool Automatic;
}
