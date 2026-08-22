using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents.Components;
using Content.Shared.Administration;
using Content.Shared.EntityTable;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing; // Starlight
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Shared.Utility;

namespace Content.Server.StationEvents
{
    /// <summary>
    ///     The basic event scheduler rule, loosely based off of /tg/ events, which most
    ///     game presets use.
    /// </summary>
    [UsedImplicitly]
    public sealed partial class BasicStationEventSchedulerSystem : GameRuleSystem<BasicStationEventSchedulerComponent>
    {
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private EventManagerSystem _event = default!;
        // Starlight-start
        [Dependency] private IGameTiming _timing = default!;

        private int _queueIdCounter;
        // Starlight-end

        /// <inheritdoc/>
        protected override void Started(EntityUid uid, BasicStationEventSchedulerComponent component, GameRuleComponent gameRule,
            GameRuleStartedEvent args)
        {
            // A little starting variance so schedulers dont all proc at once.
            component.TimeUntilNextEvent = RobustRandom.NextFloat(component.MinimumTimeUntilFirstEvent, component.MinimumTimeUntilFirstEvent + 120);
            // Starlight-start
            component.EventQueue.Clear();
            // A pause left over from the previous round would shift this fresh queue forward
            // by an interval that has nothing to do with it.
            component.PausedAt = null;
            EnsureScheduledEvents(uid, component);
            // Starlight-end
        }

        /// <inheritdoc/>
        protected override void Ended(EntityUid uid, BasicStationEventSchedulerComponent component, GameRuleComponent gameRule,
            GameRuleEndedEvent args)
        {
            component.TimeUntilNextEvent = component.MinimumTimeUntilFirstEvent;
            // Starlight-start
            component.EventQueue.Clear();
            component.PausedAt = null;
            // Starlight-end
        }

        /// <inheritdoc/>
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<BasicStationEventSchedulerComponent, GameRuleComponent>();
            while (query.MoveNext(out var uid, out var eventScheduler, out var gameRule))
            {
                if (!GameTicker.IsGameRuleActive(uid, gameRule))
                    continue;

                // Starlight-start
                // Nothing is planned or fired while events are off, and the queue is frozen
                // rather than left to go overdue in the background.
                if (!_event.EventsEnabled)
                // Starlight-end
                {
                    eventScheduler.PausedAt ??= _timing.CurTime; // Starlight
                    continue;
                }

                // Starlight-start
                ThawQueue(eventScheduler);

                // EnsureScheduledEvents already runs on every queue mutation (add, remove,
                // reschedule and fire). Here it is only needed as a retry for when no candidate
                // was available earlier, so check first: counting is O(n) over a handful of
                // entries and avoids re-sorting the queue every tick.
                if (CountAutomatic(eventScheduler) < eventScheduler.AutoQueueLookahead)
                    EnsureScheduledEvents(uid, eventScheduler);

                ProcessDueEntries(uid, eventScheduler);
                // Starlight-end
            }
        }

        /// <summary>
        // Starlight-start
        /// Gives back the time a scheduler spent with station events switched off.
        /// </summary>
        /// <remarks>
        /// Trigger times are absolute, so without this every entry that came due while events
        /// were disabled fires at once the moment they are re-enabled. Manual entries shift
        /// too: the whole queue is frozen, not just the automatic part of it.
        /// </remarks>
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

        /// <summary>
        /// Reset the scheduler spacing after auto-planning an event.
        // Starlight-end
        /// </summary>
        private void ResetTimer(BasicStationEventSchedulerComponent component)
        {
            component.TimeUntilNextEvent = component.MinMaxEventTiming.Next(_random);
        }
        // Starlight-start

        /// <summary>
        /// Every active scheduler. A preset runs several at once - general events, space
        /// traffic, meteors - and each has its own event table and its own spacing, so taking
        /// whichever one the enumerator happens to yield first surfaces an arbitrary queue and
        /// hides the rest.
        /// </summary>
        public IEnumerable<(EntityUid Uid, BasicStationEventSchedulerComponent Scheduler)> GetActiveSchedulers()
        {
            var query = EntityQueryEnumerator<BasicStationEventSchedulerComponent, GameRuleComponent>();
            while (query.MoveNext(out var uid, out var scheduler, out var rule))
            {
                if (GameTicker.IsGameRuleActive(uid, rule))
                    yield return (uid, scheduler);
            }
        }

        /// <summary>
        /// Whether any scheduler this panel can read is running.
        /// </summary>
        /// <returns>
        /// False when no scheduler is active, and also when the only active ones keep no
        /// queue - the ramping scheduler, for instance. Callers use it to decide whether
        /// queueing an event is possible at all, so counting schedulers that cannot be
        /// queued into would offer an action that quietly does nothing.
        /// </returns>
        public bool HasActiveScheduler()
        {
            foreach (var _ in GetActiveSchedulers())
                return true;

            return false;
        }

        /// <summary>
        /// Looks up a queued entry by id across every active scheduler.
        /// </summary>
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

            uid = EntityUid.Invalid;
            scheduler = default!;
            entry = default!;
            return false;
        }

        /// <summary>
        /// The combined queue of every active scheduler, ordered by trigger time. Each entry
        /// carries the name of its scheduler, because otherwise a queue holding one event in
        /// 4 minutes and another in 43 looks arbitrary: they belong to different schedulers with
        /// different spacing, and without saying so the panel just looks broken.
        /// </summary>
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

        /// <summary>
        /// Queues an event by hand, outside the scheduler's own planning.
        /// </summary>
        /// <param name="eventId">Prototype id of the event to queue.</param>
        /// <param name="delaySeconds">
        /// Seconds from now to fire it. Null lets the scheduler place it after whatever it
        /// already holds, spaced by its own minimum.
        /// </param>
        /// <returns>
        /// False when the id names no station event, or when no readable scheduler is active
        /// to hold the entry. True means the queue changed.
        /// </returns>
        public bool ScheduleEvent(string eventId, float? delaySeconds = null)
        {
            if (!_event.HasEvent(eventId))
                return false;

            // A manual event fires through AddGameRule directly regardless of which scheduler
            // holds it, so queuing it on the first active one is enough.
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

        /// <summary>
        /// Moves a queued entry earlier or later.
        /// </summary>
        /// <param name="queueId">Id of the entry, unique across every active scheduler.</param>
        /// <param name="deltaSeconds">
        /// Seconds to shift it by, negative to bring it forward. An entry pushed before the
        /// present is clamped to now rather than fired retroactively.
        /// </param>
        /// <returns>False when no entry carries that id. True means the queue changed.</returns>
        public bool AdjustScheduledEvent(int queueId, float deltaSeconds)
        {
            if (!TryFindEntry(queueId, out _, out var scheduler, out var entry))
                return false;

            entry.TriggerTime += TimeSpan.FromSeconds(deltaSeconds);
            if (entry.TriggerTime < _timing.CurTime)
                entry.TriggerTime = _timing.CurTime;

            SortQueue(scheduler);
            return true;
        }

        /// <summary>
        /// Cancels a queued entry.
        /// </summary>
        /// <param name="queueId">Id of the entry, unique across every active scheduler.</param>
        /// <returns>False when no entry carries that id. True means the queue changed.</returns>
        public bool RemoveScheduledEvent(int queueId)
        {
            if (!TryFindEntry(queueId, out var uid, out var scheduler, out var entry))
                return false;

            scheduler.EventQueue.Remove(entry);

            if (entry.Automatic)
                CloseAutomaticGap(scheduler, entry);

            EnsureScheduledEvents(uid, scheduler);
            return true;
        }

        /// <summary>
        /// Slides the automatic entries after a cancelled one earlier, closing the gap it left.
        /// </summary>
        /// <remarks>
        /// Automatic entries chain off the last one in the queue, so cancelling removed a near
        /// event and appended its replacement behind the tail. The schedule marched into the
        /// future and never recovered: a handful of cancellations pushed the next event over an
        /// hour out on a scheduler meant to fire every few minutes. Manual entries are left
        /// where they are, since an admin asked for those at a specific time.
        /// </remarks>
        private void CloseAutomaticGap(
            BasicStationEventSchedulerComponent component,
            QueuedStationEventEntry removed)
        {
            // The slot the cancelled event occupied: from whatever automatic event preceded it
            // (or from now, if it was the head) up to its own trigger time.
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

        /// <summary>
        /// Fires a queued entry immediately instead of waiting for its trigger time.
        /// </summary>
        /// <param name="queueId">Id of the entry, unique across every active scheduler.</param>
        /// <returns>
        /// Whether the event started. Note this is not the same as whether anything happened:
        /// an entry that is found is removed from the queue either way, so a false return can
        /// still mean the queue changed, and a caller refreshing only on true will show a
        /// stale queue until its next poll.
        /// </returns>
        public bool RunScheduledEventNow(int queueId)
        {
            if (!TryFindEntry(queueId, out var uid, out var scheduler, out var entry))
                return false;

            scheduler.EventQueue.Remove(entry);
            var started = _event.RunEventById(entry.EventId);
            EnsureScheduledEvents(uid, scheduler);
            return started;
        }

        /// <summary>
        /// Queue ids unique across schedulers. Each component used to keep its own counter,
        /// so two active schedulers produced the same id and a panel action could land on the
        /// wrong entry.
        /// </summary>
        private int NextQueueId() => ++_queueIdCounter;

        /// <summary>
        /// Fires or drops queued entries that have reached their scheduled trigger time.
        /// </summary>
        private void ProcessDueEntries(EntityUid uid, BasicStationEventSchedulerComponent component)
        {
            SortQueue(component);

            while (component.EventQueue.Count > 0 && component.EventQueue[0].TriggerTime <= _timing.CurTime)
            {
                var next = component.EventQueue[0];
                component.EventQueue.RemoveAt(0);

                // An automatic event was picked minutes before this moment. If conditions
                // changed and it no longer qualifies - typically because players left and it no
                // longer meets MinimumPlayers - it is dropped and the slot goes quiet.
                //
                // Deliberately no substitute is drawn for the slot. The point of showing a
                // queue is that an admin can read what is coming, walk away, and be right.
                // Silently running a different event because the queued one fell through by a
                // player or two breaks that for the sake of one more event per round, and the
                // admin has no way to tell it happened. A quiet slot is legible; a surprise is
                // not. The lookahead refills behind the tail as usual.
                //
                // Admin scheduled entries fire regardless: there the admin's intent wins.
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

        /// <summary>
        /// Ensures the event queue has enough automatically planned future events to satisfy the lookahead threshold.
        /// </summary>
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

        /// <summary>
        /// Calculates the default trigger time for a manually queued event when no specific delay was provided.
        /// </summary>
        private TimeSpan GetDefaultManualTriggerTime(BasicStationEventSchedulerComponent component)
        {
            SortQueue(component);

            if (component.EventQueue.Count == 0)
                return _timing.CurTime + TimeSpan.FromSeconds(component.MinMaxEventTiming.Min);

            return component.EventQueue[^1].TriggerTime + TimeSpan.FromSeconds(component.MinMaxEventTiming.Min);
        }

        /// <summary>
        /// Calculates the scheduled trigger time for the next automatic event in the queue.
        /// </summary>
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

        /// <summary>
        /// Simulates upcoming queue state and picks an automatic event prototype that satisfies all conditions at the projected trigger time.
        /// </summary>
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
                    _event.GetOccurrences(proto) + projectedOccurrences >= stationEvent.MaxOccurrences.Value)
                {
                    limited.Remove(proto);
                    continue;
                }

                var queuedRuns = simulatedEvents
                    .Where(ev => ev.EventId == proto.ID)
                    .Select(ev => ev.RoundTime)
                    .ToList();

                // Whether it ran is tracked apart from when, so an event that never ran is not
                // mistaken for one that ran at round time zero.
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

        /// <summary>
        /// How many automatic events the queue holds. A plain loop rather than LINQ because
        /// this runs on every Update tick.
        /// </summary>
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

        /// <summary>
        /// Sorts the scheduler's event queue by trigger time ascending, then by ID.
        /// </summary>
        private static void SortQueue(BasicStationEventSchedulerComponent component)
        {
            component.EventQueue.Sort((a, b) =>
            {
                var timeCompare = a.TriggerTime.CompareTo(b.TriggerTime);
                return timeCompare != 0 ? timeCompare : a.Id.CompareTo(b.Id);
            });
        }
        // Starlight-end
    }

    [ToolshedCommand, AdminCommand(AdminFlags.Debug)]
    public sealed class StationEventCommand : ToolshedCommand
    {
        private EventManagerSystem? _stationEvent;
        private EntityTableSystem? _entityTable;
        private IComponentFactory? _compFac;
        private IRobustRandom? _random;
        private IPrototypeManager? _protoMan;

        /// <summary>
        ///     Estimates the expected number of times an event will run over the course of X rounds, taking into account weights and
        ///     how many events are expected to run over a given timeframe for a given playercount by repeatedly simulating rounds.
        ///     Effectively /100 (if you put 100 rounds) = probability an event will run per round.
        /// </summary>
        /// <remarks>
        ///     This isn't perfect. Code path eventually goes into <see cref="EventManagerSystem.CanRun"/>, which requires
        ///     state from <see cref="GameTicker"/>. As a result, you should probably just run this locally and not doing
        ///     a real round (it won't pollute the state, but it will get contaminated by previously ran events in the actual round)
        ///     and things like `MaxOccurrences` and `ReoccurrenceDelay` won't be respected.
        ///
        ///     I consider these to not be that relevant to the analysis here though (and I don't want most uses of them
        ///     to even exist) so I think it's fine.
        /// </remarks>
        [CommandImplementation("simulate")]
        public IEnumerable<(string, float)> Simulate([CommandArgument] EntProtoId eventSchedulerProto, [CommandArgument] int rounds, [CommandArgument] int playerCount, [CommandArgument] float roundEndMean, [CommandArgument] float roundEndStdDev)
        {
            _stationEvent ??= GetSys<EventManagerSystem>();
            _entityTable ??= GetSys<EntityTableSystem>();
            _compFac ??= IoCManager.Resolve<IComponentFactory>();
            _random ??= IoCManager.Resolve<IRobustRandom>();
            _protoMan ??= IoCManager.Resolve<IPrototypeManager>();

            var occurrences = new Dictionary<string, int>();

            foreach (var ev in _stationEvent.AllEvents())
            {
                occurrences.Add(ev.Key.ID, 0);
            }

            var eventScheduler = _protoMan.Index(eventSchedulerProto);

            if (!eventScheduler.TryGetComponent<BasicStationEventSchedulerComponent>(out var basicScheduler, _compFac))
            {
                return occurrences.Select(p => (p.Key, (float)p.Value)).OrderByDescending(p => p.Item2);
            }

            var compMinMax = basicScheduler.MinMaxEventTiming; // we gotta do this since we cant execute on comp w/o an ent.

            for (var i = 0; i < rounds; i++)
            {
                var curTime = TimeSpan.Zero;
                var randomEndTime = _random.NextGaussian(roundEndMean, roundEndStdDev) * 60; // Its in minutes, should probably be a better time format once we get that in toolshed like [hh:mm:ss]
                if (randomEndTime <= 0)
                    continue;

                while (curTime.TotalSeconds < randomEndTime)
                {
                    // sim an event
                    curTime += TimeSpan.FromSeconds(compMinMax.Next(_random));

                    var available = _stationEvent.AvailableEvents(false, playerCount, curTime);
                    if (!_stationEvent.TryBuildLimitedEvents(basicScheduler.ScheduledGameRules, available, out var selectedEvents))
                    {
                        continue; // doesnt break because maybe the time is preventing events being available.
                    }

                    var ev = _stationEvent.FindEvent(selectedEvents);
                    if (ev == null)
                        continue;

                    occurrences[ev] += 1;
                }
            }

            return occurrences.Select(p => (p.Key, (float)p.Value)).OrderByDescending(p => p.Item2);
        }

        /// <summary>
        /// Lists the current proportional probability of each event for a given scheduler prototype.
        /// </summary>
        [CommandImplementation("lsprob")]
        public IEnumerable<(string, float)> LsProb([CommandArgument] EntProtoId eventSchedulerProto)
        {
            _compFac ??= IoCManager.Resolve<IComponentFactory>();
            _stationEvent ??= GetSys<EventManagerSystem>();
            _protoMan ??= IoCManager.Resolve<IPrototypeManager>();

            var eventScheduler = _protoMan.Index(eventSchedulerProto);

            if (!eventScheduler.TryGetComponent<BasicStationEventSchedulerComponent>(out var basicScheduler, _compFac))
                yield break;

            var available = _stationEvent.AvailableEvents();
            if (!_stationEvent.TryBuildLimitedEvents(basicScheduler.ScheduledGameRules, available, out var events))
                yield break;

            var totalWeight = events.Sum(x => x.Value.Weight); // Well this shit definitely isnt correct now, and I see no way to make it correct.
                                                               // Its probably *fine* but it wont be accurate if the EntityTableSelector does any subsetting.
            foreach (var (proto, comp) in events)              // The only solution I see is to do a simulation, and we already have that, so...!
            {
                yield return (proto.ID, comp.Weight / totalWeight);
            }
        }

        /// <summary>
        /// Lists the theoretical probability of each event for a given scheduler at a specified player count and round time.
        /// </summary>
        [CommandImplementation("lsprobtheoretical")]
        public IEnumerable<(string, float)> LsProbTime([CommandArgument] EntProtoId eventSchedulerProto, [CommandArgument] int playerCount, [CommandArgument] float time)
        {
            _compFac ??= IoCManager.Resolve<IComponentFactory>();
            _stationEvent ??= GetSys<EventManagerSystem>();
            _protoMan ??= IoCManager.Resolve<IPrototypeManager>();

            var eventScheduler = _protoMan.Index(eventSchedulerProto);

            if (!eventScheduler.TryGetComponent<BasicStationEventSchedulerComponent>(out var basicScheduler, _compFac))
                yield break;

            var timemins = time * 60;
            var theoryTime = TimeSpan.Zero + TimeSpan.FromSeconds(timemins);
            var available = _stationEvent.AvailableEvents(false, playerCount, theoryTime);
            if (!_stationEvent.TryBuildLimitedEvents(basicScheduler.ScheduledGameRules, available, out var untimedEvents))
                yield break;

            var events = untimedEvents.Where(pair => pair.Value.EarliestStart <= timemins).ToList();

            var totalWeight = events.Sum(x => x.Value.Weight); // same subsetting issue as lsprob.

            foreach (var (proto, comp) in events)
            {
                yield return (proto.ID, comp.Weight / totalWeight);
            }
        }

        /// <summary>
        /// Calculates the current selection probability for a specific event ID on a given scheduler prototype.
        /// </summary>
        [CommandImplementation("prob")]
        public float Prob([CommandArgument] EntProtoId eventSchedulerProto, [CommandArgument] string eventId)
        {
            _compFac ??= IoCManager.Resolve<IComponentFactory>();
            _stationEvent ??= GetSys<EventManagerSystem>();
            _protoMan ??= IoCManager.Resolve<IPrototypeManager>();

            var eventScheduler = _protoMan.Index(eventSchedulerProto);

            if (!eventScheduler.TryGetComponent<BasicStationEventSchedulerComponent>(out var basicScheduler, _compFac))
                return 0f;

            var available = _stationEvent.AvailableEvents();
            if (!_stationEvent.TryBuildLimitedEvents(basicScheduler.ScheduledGameRules, available, out var events))
                return 0f;

            var totalWeight = events.Sum(x => x.Value.Weight); // same subsetting issue as lsprob.
            var weight = 0f;
            if (events.TryFirstOrNull(p => p.Key.ID == eventId, out var pair))
            {
                weight = pair.Value.Value.Weight;
            }

            return weight / totalWeight;
        }
    }
}
