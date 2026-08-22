using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Administration.Events;

/// <summary>
/// Serializable data transfer object describing a station event prototype and its current status.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationEventData
{
    public string Id = string.Empty;
    public bool Available;
    public int MinimumPlayers;
    public int EarliestStartMinutes;
    public int ReoccurrenceDelayMinutes;
    public float Weight;
    public float DurationSeconds = -1f;
    public float MaxDurationSeconds = -1f;
    public int ActiveCount;
    public int PendingCount;
    public float NextStartSeconds = -1f;
    public float MinRemainingSeconds = -1f;
    public float MaxRemainingSeconds = -1f;

    /// <summary>
    ///     How many times this event has already run this round.
    /// </summary>
    /// <remarks>
    ///     Shown alongside the weight because with repetition falloff enabled the weight is
    ///     derived from this, and a decayed weight is otherwise unexplained.
    /// </remarks>
    public int Occurrences;
}

/// <summary>
/// Serializable data transfer object describing a scheduled future station event in the queue.
/// </summary>
[Serializable, NetSerializable]
public sealed class ScheduledStationEventData
{
    public int Id;
    public string EventId = string.Empty;
    public float TriggerInSeconds;
    public float TotalDelaySeconds;
    public bool Automatic;

    /// <summary>
    ///     Prototype of the scheduler holding this entry. A preset runs several at once with
    ///     very different spacing, so without this the combined queue is unreadable.
    /// </summary>
    public string Scheduler = string.Empty;
}

/// <summary>
/// Serializable data transfer object describing an active running station event gamerule.
/// </summary>
[Serializable, NetSerializable]
public sealed class ActiveStationEventData
{
    public NetEntity Entity = NetEntity.Invalid;
    public string EventId = string.Empty;
    public float ElapsedSeconds;
    public float DurationSeconds = -1f;
    public float RemainingSeconds = -1f;
}

/// <summary>
/// Network event sent by the server to update the admin station events tab.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationEventsChangedEvent : EntityEventArgs
{
    public List<StationEventData> Events = new();
    public List<ScheduledStationEventData> Queue = new();
    public List<ActiveStationEventData> ActiveEvents = new();
    public bool EventsEnabled;
    public int PlayerCount;
    public float RoundDurationMinutes;
    public bool HasScheduler;

    /// <summary>
    ///     Active schedulers whose queue this panel cannot read because they use a different
    ///     component (the ramping one, for instance). When greater than zero the queue shown is
    ///     incomplete, and saying so beats implying everything is visible.
    /// </summary>
    public int UnreadableSchedulers;
}

/// <summary>
/// Network event sent by the client to request a fresh station events snapshot.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestStationEventsEvent : EntityEventArgs
{
}

/// <summary>
/// Action types for managing the station event queue and active events.
/// </summary>
[Serializable, NetSerializable]
public enum StationEventQueueCommand
{
    Schedule,
    Adjust,
    Remove,
    RunNow,
    EndActive
}

/// <summary>
/// Network event sent by the client to trigger an admin action on the station event queue.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationEventQueueCommandEvent : EntityEventArgs
{
    public StationEventQueueCommand Command;
    public string EventId = string.Empty;
    public int QueueId;
    public float Seconds = -1f;
    public NetEntity ActiveEvent = NetEntity.Invalid;
}
