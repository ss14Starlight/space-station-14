using Robust.Shared.Serialization;

namespace Content.Shared.Administration.Events;

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
}

[Serializable, NetSerializable]
public sealed class ScheduledStationEventData
{
    public int Id;
    public string EventId = string.Empty;
    public float TriggerInSeconds;
    public float TotalDelaySeconds;
    public bool Automatic;
}

[Serializable, NetSerializable]
public sealed class ActiveStationEventData
{
    public NetEntity Entity = NetEntity.Invalid;
    public string EventId = string.Empty;
    public float ElapsedSeconds;
    public float DurationSeconds = -1f;
    public float RemainingSeconds = -1f;
}

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
}

[Serializable, NetSerializable]
public sealed class RequestStationEventsEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public enum StationEventQueueCommand
{
    Schedule,
    Adjust,
    Remove,
    RunNow,
    EndActive
}

[Serializable, NetSerializable]
public sealed class StationEventQueueCommandEvent : EntityEventArgs
{
    public StationEventQueueCommand Command;
    public string EventId = string.Empty;
    public int QueueId;
    public float Seconds = -1f;
    public NetEntity ActiveEvent = NetEntity.Invalid;
}
