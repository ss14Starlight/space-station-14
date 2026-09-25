using System.Linq;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Timing;

// ReSharper disable once CheckNamespace
namespace Content.Server.StationEvents;

public sealed partial class BasicStationEventSchedulerSystem
{
    [Dependency] private IGameTiming _timing = default!;
    private int _queueIdCounter;

    private void InitializeEventQueue(EntityUid uid, BasicStationEventSchedulerComponent component)
    {
        component.EventQueue.Clear();
        component.PausedAt = null;
        EnsureScheduledEvents(uid, component);
    }

    private static void ClearEventQueue(BasicStationEventSchedulerComponent component)
    {
        component.EventQueue.Clear();
        component.PausedAt = null;
    }

    private void ProcessQueuedEvents()
    {
        var query = EntityQueryEnumerator<BasicStationEventSchedulerComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var scheduler, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            if (!_event.EventsEnabled)
            {
                scheduler.PausedAt ??= _timing.CurTime;
                continue;
            }

            ThawQueue(scheduler);
            if (CountAutomatic(scheduler) < scheduler.AutoQueueLookahead)
                EnsureScheduledEvents(uid, scheduler);
            ProcessDueEntries(uid, scheduler);
        }
    }

        private void ThawQueue(BasicStationEventSchedulerComponent component)
        {
            if (component.PausedAt is not { } pausedAt)
                return;

            var frozen = _timing.CurTime - pausedAt;
            component.PausedAt = null;

            if (frozen <= TimeSpan.Zero)
                return;

            foreach (var entry in component.EventQueue)
                entry.TriggerTime += frozen;
        }

        /// <summary>Enumerates active basic event schedulers.</summary>
        public IEnumerable<(EntityUid Uid, BasicStationEventSchedulerComponent Scheduler)> GetActiveSchedulers()
        {
            var query = EntityQueryEnumerator<BasicStationEventSchedulerComponent, GameRuleComponent>();
            while (query.MoveNext(out var uid, out var scheduler, out var rule))
            {
                if (GameTicker.IsGameRuleActive(uid, rule))
                    yield return (uid, scheduler);
            }
        }

        /// <summary>Checks for an active basic scheduler.</summary>
        public bool HasActiveScheduler()
        {
            foreach (var _ in GetActiveSchedulers())
                return true;

            return false;
        }

        private bool TryFindEntry(
            int queueId,
            out EntityUid uid,
            out BasicStationEventSchedulerComponent scheduler,
            out QueuedStationEventEntry entry)
        {
            foreach (var (candidateUid, candidate) in GetActiveSchedulers())
            {
                foreach (var candidateEntry in candidate.EventQueue)
                {
                    if (candidateEntry.Id != queueId)
                        continue;

                    uid = candidateUid;
                    scheduler = candidate;
                    entry = candidateEntry;
                    return true;
                }
            }

            uid = default;
            scheduler = default!;
            entry = default!;
            return false;
        }

        /// <summary>Returns queued events across active schedulers.</summary>
        public IReadOnlyList<(QueuedStationEventEntry Entry, string Scheduler)> GetQueuedEvents()
        {
            var combined = new List<(QueuedStationEventEntry Entry, string Scheduler)>();

            foreach (var (uid, scheduler) in GetActiveSchedulers())
            {
                SortQueue(scheduler);
                var name = MetaData(uid).EntityPrototype?.ID ?? "Unknown";
                foreach (var entry in scheduler.EventQueue)
                    combined.Add((entry, name));
            }

            combined.Sort((a, b) =>
            {
                var timeCompare = a.Entry.TriggerTime.CompareTo(b.Entry.TriggerTime);
                return timeCompare != 0 ? timeCompare : a.Entry.Id.CompareTo(b.Entry.Id);
            });

            return combined;
        }

        /// <summary>Queues an event manually.</summary>
        public bool TryScheduleEvent(string eventId, float? delaySeconds = null)
        {
            if (!_event.HasEvent(eventId) ||
                delaySeconds is { } delay && (!float.IsFinite(delay) || delay > int.MaxValue))
                return false;
            foreach (var (uid, scheduler) in GetActiveSchedulers())
            {
                var triggerTime = delaySeconds.HasValue
                    ? _timing.CurTime + TimeSpan.FromSeconds(Math.Max(delaySeconds.Value, 0f))
                    : GetDefaultManualTriggerTime(scheduler);

                scheduler.EventQueue.Add(new QueuedStationEventEntry
                {
                    Id = NextQueueId(),
                    EventId = eventId,
                    QueuedAt = _timing.CurTime,
                    TriggerTime = triggerTime,
                    Automatic = false
                });

                SortQueue(scheduler);
                EnsureScheduledEvents(uid, scheduler);
                return true;
            }

            return false;
        }

        /// <summary>Adjusts the trigger time of a queued event.</summary>
        public bool TryAdjustScheduledEvent(int queueId, float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || Math.Abs(deltaSeconds) > int.MaxValue)
                return false;

            if (!TryFindEntry(queueId, out _, out var scheduler, out var entry))
                return false;

            entry.TriggerTime += TimeSpan.FromSeconds(deltaSeconds);
            if (entry.TriggerTime < _timing.CurTime)
                entry.TriggerTime = _timing.CurTime;

            SortQueue(scheduler);
            return true;
        }

        /// <summary>Removes a queued event.</summary>
        public bool TryRemoveScheduledEvent(int queueId)
        {
            if (!TryFindEntry(queueId, out var uid, out var scheduler, out var entry))
                return false;

            scheduler.EventQueue.Remove(entry);

            if (entry.Automatic)
                CloseAutomaticGap(scheduler, entry);

            EnsureScheduledEvents(uid, scheduler);
            return true;
        }

        private void CloseAutomaticGap(
            BasicStationEventSchedulerComponent component,
            QueuedStationEventEntry removed)
        {
            var previous = _timing.CurTime;
            foreach (var candidate in component.EventQueue)
            {
                if (candidate.Automatic &&
                    candidate.TriggerTime < removed.TriggerTime &&
                    candidate.TriggerTime > previous)
                {
                    previous = candidate.TriggerTime;
                }
            }

            var gap = removed.TriggerTime - previous;
            if (gap <= TimeSpan.Zero)
                return;

            foreach (var candidate in component.EventQueue)
            {
                if (!candidate.Automatic || candidate.TriggerTime <= removed.TriggerTime)
                    continue;

                candidate.TriggerTime -= gap;
                if (candidate.TriggerTime < _timing.CurTime)
                    candidate.TriggerTime = _timing.CurTime;
            }
        }

        /// <summary>Runs a queued event immediately.</summary>
        public bool TryRunScheduledEventNow(int queueId)
        {
            if (!TryFindEntry(queueId, out var uid, out var scheduler, out var entry))
                return false;

            scheduler.EventQueue.Remove(entry);
            if (!_event.RunEventById(entry.EventId))
                Log.Warning($"Queued event {entry.EventId} failed to start.");
            EnsureScheduledEvents(uid, scheduler);
            return true;
        }

        private int NextQueueId() => ++_queueIdCounter;

        private void ProcessDueEntries(EntityUid uid, BasicStationEventSchedulerComponent component)
        {
            SortQueue(component);

            while (component.EventQueue.Count > 0 && component.EventQueue[0].TriggerTime <= _timing.CurTime)
            {
                var next = component.EventQueue[0];
                component.EventQueue.RemoveAt(0);
                if (next.Automatic && !_event.CanRunNow(next.EventId))
                {
                    Log.Debug($"Dropped automatic event {next.EventId} on trigger: no longer eligible.");
                    EnsureScheduledEvents(uid, component);
                    SortQueue(component);
                    continue;
                }

                if (!_event.RunEventById(next.EventId))
                    Log.Warning($"Queued event {next.EventId} came due but failed to start.");

                EnsureScheduledEvents(uid, component);
                SortQueue(component);
            }
        }

        private void EnsureScheduledEvents(EntityUid uid, BasicStationEventSchedulerComponent component)
        {
            SortQueue(component);

            while (CountAutomatic(component) < component.AutoQueueLookahead)
            {
                var triggerTime = GetNextAutomaticTriggerTime(component);
                if (!TryPickAutomaticEvent(component, triggerTime, out var eventId))
                    break;

                component.EventQueue.Add(new QueuedStationEventEntry
                {
                    Id = NextQueueId(),
                    EventId = eventId,
                    QueuedAt = _timing.CurTime,
                    TriggerTime = triggerTime,
                    Automatic = true
                });

                ResetTimer(component);
                SortQueue(component);
            }
        }

        private TimeSpan GetDefaultManualTriggerTime(BasicStationEventSchedulerComponent component)
        {
            SortQueue(component);

            if (component.EventQueue.Count == 0)
                return _timing.CurTime + TimeSpan.FromSeconds(component.MinMaxEventTiming.Min);

            return component.EventQueue[^1].TriggerTime + TimeSpan.FromSeconds(component.MinMaxEventTiming.Min);
        }

        private TimeSpan GetNextAutomaticTriggerTime(BasicStationEventSchedulerComponent component)
        {
            var lastAutomatic = component.EventQueue
                .Where(ev => ev.Automatic)
                .OrderBy(ev => ev.TriggerTime)
                .LastOrDefault();

            if (lastAutomatic != null)
                return lastAutomatic.TriggerTime + TimeSpan.FromSeconds(component.TimeUntilNextEvent);

            return _timing.CurTime + TimeSpan.FromSeconds(component.TimeUntilNextEvent);
        }

        private bool TryPickAutomaticEvent(
            BasicStationEventSchedulerComponent component,
            TimeSpan triggerTime,
            out string eventId)
        {
            var projectedRoundTime = GameTicker.RoundDuration() + (triggerTime - _timing.CurTime);
            var available = _event.AvailableEvents(currentTimeOverride: projectedRoundTime);

            if (!_event.TryBuildLimitedEvents(component.ScheduledGameRules, available, out var limited))
            {
                eventId = string.Empty;
                return false;
            }

            var simulatedEvents = component.EventQueue
                .Where(ev => ev.TriggerTime <= triggerTime)
                .Select(ev => (ev.EventId, RoundTime: GameTicker.RoundDuration() + (ev.TriggerTime - _timing.CurTime)))
                .ToList();

            foreach (var (proto, stationEvent) in limited.ToList())
            {
                var projectedOccurrences = simulatedEvents.Count(ev => ev.EventId == proto.ID);
                if (stationEvent.MaxOccurrences.HasValue &&
                    _event.GetStationEventOccurrences(proto) + projectedOccurrences >= stationEvent.MaxOccurrences.Value)
                {
                    limited.Remove(proto);
                    continue;
                }

                var queuedRuns = simulatedEvents
                    .Where(ev => ev.EventId == proto.ID)
                    .Select(ev => ev.RoundTime)
                    .ToList();
                var ranBefore = _event.TryGetLastEventTime(proto, out var actualLastRun);
                if (queuedRuns.Count == 0 && !ranBefore)
                    continue;

                var queuedLastRun = queuedRuns.Count > 0 ? queuedRuns.Max() : TimeSpan.Zero;
                var effectiveLastRun = queuedLastRun > actualLastRun ? queuedLastRun : actualLastRun;

                if (projectedRoundTime.TotalMinutes < stationEvent.ReoccurrenceDelay + effectiveLastRun.TotalMinutes)
                {
                    limited.Remove(proto);
                }
            }

            eventId = _event.FindEvent(limited) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(eventId);
        }

        private static int CountAutomatic(BasicStationEventSchedulerComponent component)
        {
            var total = 0;
            foreach (var entry in component.EventQueue)
            {
                if (entry.Automatic)
                    total++;
            }

            return total;
        }

        private static void SortQueue(BasicStationEventSchedulerComponent component)
        {
            component.EventQueue.Sort((a, b) =>
            {
                var timeCompare = a.TriggerTime.CompareTo(b.TriggerTime);
                return timeCompare != 0 ? timeCompare : a.Id.CompareTo(b.Id);
            });
        }
}
