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
using Robust.Shared.Timing;
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
        [Dependency] private IGameTiming _timing = default!;

        private int _queueIdCounter;

        protected override void Started(EntityUid uid, BasicStationEventSchedulerComponent component, GameRuleComponent gameRule,
            GameRuleStartedEvent args)
        {
            // A little starting variance so schedulers dont all proc at once.
            component.TimeUntilNextEvent = RobustRandom.NextFloat(component.MinimumTimeUntilFirstEvent, component.MinimumTimeUntilFirstEvent + 120);
            component.EventQueue.Clear();
            EnsureScheduledEvents(uid, component);
        }

        protected override void Ended(EntityUid uid, BasicStationEventSchedulerComponent component, GameRuleComponent gameRule,
            GameRuleEndedEvent args)
        {
            component.TimeUntilNextEvent = component.MinimumTimeUntilFirstEvent;
            component.EventQueue.Clear();
        }


        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<BasicStationEventSchedulerComponent, GameRuleComponent>();
            while (query.MoveNext(out var uid, out var eventScheduler, out var gameRule))
            {
                if (!GameTicker.IsGameRuleActive(uid, gameRule))
                    continue;

                // EnsureScheduledEvents ya corre en cada mutacion de la cola (alta, baja,
                // adelanto y disparo). Aca solo hace falta como reintento para cuando antes
                // no habia candidatos disponibles, asi que se chequea primero: contar es O(n)
                // sobre unos pocos elementos y evita reordenar la cola en cada tick.
                if (CountAutomatic(eventScheduler) < eventScheduler.AutoQueueLookahead)
                    EnsureScheduledEvents(uid, eventScheduler);

                if (!_event.EventsEnabled)
                    continue;

                ProcessDueEntries(uid, eventScheduler);
            }
        }

        /// <summary>
        /// Reset the scheduler spacing after auto-planning an event.
        /// </summary>
        private void ResetTimer(BasicStationEventSchedulerComponent component)
        {
            component.TimeUntilNextEvent = component.MinMaxEventTiming.Next(_random);
        }

        /// <summary>
        /// Todos los schedulers activos. Un preset corre varios a la vez -eventos generales,
        /// trafico espacial, meteoritos- y cada uno tiene su propia tabla de eventos y su propio
        /// espaciado, asi que quedarse con el primero que aparece muestra una cola arbitraria
        /// y esconde el resto.
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

        public bool HasActiveScheduler()
        {
            foreach (var _ in GetActiveSchedulers())
                return true;

            return false;
        }

        /// <summary>
        /// Busca una entrada por id en cualquiera de los schedulers activos.
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
        /// La cola combinada de todos los schedulers activos, ordenada por tiempo de disparo.
        /// Cada entrada viaja con el nombre de su scheduler, porque si no una cola con un evento
        /// en 4 minutos y otro en 43 se ve arbitraria: son schedulers distintos con espaciados
        /// distintos, y sin decirlo el panel parece roto.
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

        public bool ScheduleEvent(string eventId, float? delaySeconds = null)
        {
            if (!_event.HasEvent(eventId))
                return false;

            // Un evento manual se dispara con AddGameRule directo, sin importar que scheduler lo
            // tenga encolado, asi que alcanza con ponerlo en el primero activo.
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

        public bool RemoveScheduledEvent(int queueId)
        {
            if (!TryFindEntry(queueId, out var uid, out var scheduler, out var entry))
                return false;

            scheduler.EventQueue.Remove(entry);
            EnsureScheduledEvents(uid, scheduler);
            return true;
        }

        public bool RunScheduledEventNow(int queueId)
        {
            if (!TryFindEntry(queueId, out var uid, out var scheduler, out var entry))
                return false;

            scheduler.EventQueue.Remove(entry);
            _event.RunEventById(entry.EventId);
            EnsureScheduledEvents(uid, scheduler);
            return true;
        }

        /// <summary>
        /// Ids unicos entre schedulers. Antes cada componente llevaba su propio contador, asi
        /// que dos schedulers activos generaban el mismo id y una accion del panel podia caer
        /// sobre la entrada equivocada.
        /// </summary>
        private int NextQueueId() => ++_queueIdCounter;

        private void ProcessDueEntries(EntityUid uid, BasicStationEventSchedulerComponent component)
        {
            SortQueue(component);

            while (component.EventQueue.Count > 0 && component.EventQueue[0].TriggerTime <= _timing.CurTime)
            {
                var next = component.EventQueue[0];
                component.EventQueue.RemoveAt(0);

                // Un evento automatico se eligio minutos antes de este momento. Si las
                // condiciones cambiaron y ya no corresponde -tipicamente porque se fueron
                // jugadores y no llega a MinimumPlayers- se descarta y el lookahead repone
                // otro. Los programados por un admin disparan igual: ahi manda su intencion.
                if (next.Automatic && !_event.CanRunNow(next.EventId))
                {
                    Log.Debug($"Evento automatico {next.EventId} descartado al vencer: ya no cumple condiciones.");
                    EnsureScheduledEvents(uid, component);
                    SortQueue(component);
                    continue;
                }

                _event.RunEventById(next.EventId);
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
                    _event.GetOccurrences(proto) + projectedOccurrences >= stationEvent.MaxOccurrences.Value)
                {
                    limited.Remove(proto);
                    continue;
                }

                var queuedLastRun = simulatedEvents
                    .Where(ev => ev.EventId == proto.ID)
                    .Select(ev => ev.RoundTime)
                    .DefaultIfEmpty(TimeSpan.Zero)
                    .Max();

                var actualLastRun = _event.TimeSinceLastEvent(proto);
                var effectiveLastRun = queuedLastRun > actualLastRun ? queuedLastRun : actualLastRun;

                if (effectiveLastRun != TimeSpan.Zero &&
                    projectedRoundTime.TotalMinutes < stationEvent.ReoccurrenceDelay + effectiveLastRun.TotalMinutes)
                {
                    limited.Remove(proto);
                }
            }

            eventId = _event.FindEvent(limited) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(eventId);
        }

        /// <summary>
        /// Cuantos eventos automaticos hay en la cola. Es un for y no LINQ porque esto corre
        /// en cada tick del Update.
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

        private static void SortQueue(BasicStationEventSchedulerComponent component)
        {
            component.EventQueue.Sort((a, b) =>
            {
                var timeCompare = a.TriggerTime.CompareTo(b.TriggerTime);
                return timeCompare != 0 ? timeCompare : a.Id.CompareTo(b.Id);
            });
        }
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
