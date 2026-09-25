using System.Linq;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Shared._Starlight.Administration.Events;
using Content.Shared.Administration;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

// ReSharper disable once CheckNamespace
namespace Content.Server.Administration.Systems;

public sealed partial class AdminSystem
{
    [Dependency] private EventManagerSystem _eventManager = default!;
    [Dependency] private BasicStationEventSchedulerSystem _eventScheduler = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan StationEventsRequestInterval = TimeSpan.FromSeconds(0.75);
    private readonly Dictionary<NetUserId, TimeSpan> _lastStationEventsRequest = new();

    private void ClearStationEventsRequests() => _lastStationEventsRequest.Clear();

    private void OnRequestStationEvents(RequestStationEventsEvent ev, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin))
            return;

        var now = _timing.CurTime;
        var user = args.SenderSession.UserId;
        if (_lastStationEventsRequest.TryGetValue(user, out var last) &&
            now - last < StationEventsRequestInterval)
        {
            return;
        }

        _lastStationEventsRequest[user] = now;
        SendStationEvents(args.SenderSession);
    }

    private void OnStationEventQueueCommand(StationEventQueueCommandEvent ev, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
            return;

        var changed = ev.Command switch
        {
            StationEventQueueCommand.Schedule =>
                _eventScheduler.TryScheduleEvent(ev.EventId, ev.Seconds < 0f ? null : ev.Seconds),
            StationEventQueueCommand.Adjust =>
                _eventScheduler.TryAdjustScheduledEvent(ev.QueueId, ev.Seconds),
            StationEventQueueCommand.Remove =>
                _eventScheduler.TryRemoveScheduledEvent(ev.QueueId),
            StationEventQueueCommand.RunNow =>
                _eventScheduler.TryRunScheduledEventNow(ev.QueueId),
            StationEventQueueCommand.EndActive => EndActiveStationEvent(ev.ActiveEvent),
            _ => false
        };

        if (changed)
            SendStationEvents(args.SenderSession);
    }

    private bool EndActiveStationEvent(NetEntity netEntity)
    {
        var uid = GetEntity(netEntity);
        if (!Exists(uid) ||
            !HasComp<StationEventComponent>(uid) ||
            !_gameTicker.IsGameRuleActive(uid))
        {
            return false;
        }

        return _gameTicker.EndGameRule(uid);
    }

    private int CountUnreadableSchedulers()
    {
        var count = 0;
        var query = EntityQueryEnumerator<RampingStationEventSchedulerComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var rule))
        {
            if (_gameTicker.IsGameRuleActive(uid, rule))
                count++;
        }

        return count;
    }

    private void SendStationEvents(ICommonSession session)
    {
        var available = _eventManager.AvailableEvents();
        
        var runtimeStates = new Dictionary<string, EventRuntimeState>();
        var activeEvents = new List<ActiveStationEventData>();

        foreach (var uid in _gameTicker.GetAddedGameRules())
        {
            if (MetaData(uid).EntityPrototype?.ID is not { } id ||
                !TryComp(uid, out StationEventComponent? stationEvent) ||
                HasComp<EndedGameRuleComponent>(uid))
            {
                continue;
            }

            if (!runtimeStates.TryGetValue(id, out var runtime))
            {
                runtime = new EventRuntimeState();
                runtimeStates[id] = runtime;
            }

            if (_gameTicker.IsGameRuleActive(uid))
            {
                runtime.ActiveCount++;

                var elapsed = 0f;
                if (TryComp(uid, out GameRuleComponent? gameRule))
                {
                    elapsed = Math.Max((float) (_timing.CurTime - gameRule.ActivatedAt).TotalSeconds, 0f);
                }

                var duration = -1f;
                var remaining = -1f;

                if (stationEvent.EndTime is { } endTime)
                {
                    remaining = Math.Max((float) (endTime - _timing.CurTime).TotalSeconds, 0f);
                    duration = elapsed + remaining;
                    runtime.MinRemainingSeconds = runtime.MinRemainingSeconds < 0f
                        ? remaining
                        : Math.Min(runtime.MinRemainingSeconds, remaining);
                    runtime.MaxRemainingSeconds = Math.Max(runtime.MaxRemainingSeconds, remaining);
                }

                activeEvents.Add(new ActiveStationEventData
                {
                    Entity = GetNetEntity(uid),
                    EventId = id,
                    ElapsedSeconds = elapsed,
                    DurationSeconds = duration,
                    RemainingSeconds = remaining
                });

                continue;
            }

            runtime.PendingCount++;

            var nextStart = 0f;
            DelayedStartRuleComponent? delayed = null;
            if (TryComp(uid, out delayed) && delayed != null)
                nextStart = Math.Max((float) (delayed.RuleStartTime - _timing.CurTime).TotalSeconds, 0f);

            runtime.NextStartSeconds = runtime.NextStartSeconds < 0f
                ? nextStart
                : Math.Min(runtime.NextStartSeconds, nextStart);
        }

        var snapshot = new StationEventsChangedEvent
        {
            EventsEnabled = _eventManager.EventsEnabled,
            PlayerCount = _playerManager.PlayerCount,
            RoundDurationMinutes = (float) _gameTicker.RoundDuration().TotalMinutes,
            HasScheduler = _eventScheduler.HasActiveScheduler(),
            UnreadableSchedulers = CountUnreadableSchedulers(),
            Queue = _eventScheduler.GetQueuedEvents()
                .Select(queued => new ScheduledStationEventData
                {
                    Id = queued.Entry.Id,
                    EventId = queued.Entry.EventId,
                    TriggerInSeconds = Math.Max((float) (queued.Entry.TriggerTime - _timing.CurTime).TotalSeconds, 0f),
                    TotalDelaySeconds = Math.Max((float) (queued.Entry.TriggerTime - queued.Entry.QueuedAt).TotalSeconds, 0f),
                    Automatic = queued.Entry.Automatic,
                    Scheduler = queued.Scheduler
                })
                .ToList(),
            ActiveEvents = activeEvents
                .OrderBy(active => active.RemainingSeconds < 0f ? float.MaxValue : active.RemainingSeconds)
                .ThenBy(active => active.EventId)
                .ToList(),
            Events = _eventManager.AllEvents()
                .OrderBy(pair => pair.Key.ID)
                .Select(pair =>
                {
                    var runtime = runtimeStates.GetValueOrDefault(pair.Key.ID);

                    return new StationEventData
                    {
                        Id = pair.Key.ID,
                        Available = available.ContainsKey(pair.Key),
                        MinimumPlayers = pair.Value.MinimumPlayers,
                        EarliestStartMinutes = pair.Value.EarliestStart,
                        ReoccurrenceDelayMinutes = pair.Value.ReoccurrenceDelay,
                        Weight = pair.Value.Weight,
                        DurationSeconds = pair.Value.Duration is { } duration
                            ? (float) duration.TotalSeconds
                            : -1f,
                        MaxDurationSeconds = pair.Value.MaxDuration is { } maxDuration
                            ? (float) maxDuration.TotalSeconds
                            : pair.Value.Duration is { } fixedDuration
                                ? (float) fixedDuration.TotalSeconds
                                : -1f,
                        ActiveCount = runtime?.ActiveCount ?? 0,
                        PendingCount = runtime?.PendingCount ?? 0,
                        NextStartSeconds = runtime?.NextStartSeconds ?? -1f,
                        MinRemainingSeconds = runtime?.MinRemainingSeconds ?? -1f,
                        MaxRemainingSeconds = runtime?.MaxRemainingSeconds ?? -1f
                    };
                })
                .ToList()
        };

        RaiseNetworkEvent(snapshot, session.Channel);
    }

    private sealed class EventRuntimeState
    {
        public int ActiveCount;
        public int PendingCount;
        public float NextStartSeconds = -1f;
        public float MinRemainingSeconds = -1f;
        public float MaxRemainingSeconds = -1f;
    }
}
