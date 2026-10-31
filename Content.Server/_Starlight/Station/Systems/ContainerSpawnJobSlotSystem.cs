using Content.Server.GameTicking.Events;
using Content.Server.Mind;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Intellicard;
using Content.Shared.Roles;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Station.Systems;

/// <summary>
/// Caps container-spawn job slots to their unoccupied spawn points and Station AI core availability.
/// </summary>
public sealed partial class ContainerSpawnJobSlotSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _station = default!;

    private static readonly TimeSpan _refreshCooldown = TimeSpan.FromSeconds(5);

    private readonly Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int>> _freeContainers = new();
    private readonly Dictionary<EntityUid, int> _emptyAiCores = new();
    private readonly ProtoId<JobPrototype> _stationAiJob = "StationAi";

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextTick)
            return;

        _nextTick = _timing.CurTime + _refreshCooldown;

        RefreshJobSlots();
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev) => RefreshJobSlots();

    /// <summary>
    /// Clamps every container bound job slot to the number of unoccupied spawn containers it has left.
    /// </summary>
    public void RefreshJobSlots(bool allowAiSlotIncrease = true)
    {
        _nextTick = _timing.CurTime + _refreshCooldown;

        _freeContainers.Clear();
        _emptyAiCores.Clear();

        var coreQuery = EntityQueryEnumerator<StationAiCoreComponent, ContainerManagerComponent, TransformComponent>();
        while (coreQuery.MoveNext(out var uid, out _, out var manager, out var xform))
        {
            if (_station.GetOwningStation(uid, xform) is not { } station
                || !_container.TryGetContainer(uid, StationAiCoreComponent.Container, out var aiContainer, manager))
                continue;

            if (aiContainer.ContainedEntities.Count == 0)
            {
                if (_emptyAiCores.TryGetValue(station, out var count))
                    _emptyAiCores[station] = count + 1;
                else
                    _emptyAiCores[station] = 1;
            }

            if (!_freeContainers.TryGetValue(station, out var stationJobsFreeContainers))
            {
                stationJobsFreeContainers = new Dictionary<ProtoId<JobPrototype>, int>();
                _freeContainers[station] = stationJobsFreeContainers;
            }

            stationJobsFreeContainers.TryAdd(_stationAiJob, 0);
        }

        var query = EntityQueryEnumerator<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawnPoint, out var manager, out var xform))
        {
            if (spawnPoint.Job is not { } job)
                continue;

            if (_station.GetOwningStation(uid, xform) is not { } station)
                continue;

            if (!_freeContainers.TryGetValue(station, out var jobs))
            {
                jobs = new Dictionary<ProtoId<JobPrototype>, int>();
                _freeContainers[station] = jobs;
            }

            jobs.TryAdd(job, 0);

            if (_container.TryGetContainer(uid, spawnPoint.ContainerId, out var container, manager)
                && container.ContainedEntities.Count == 0)
                jobs[job]++;
        }

        foreach (var (station, jobs) in _freeContainers)
        {
            if (!TryComp<StationJobsComponent>(station, out var stationJobs))
                continue;

            foreach (var (job, free) in jobs)
            {
                var availableSlots = free;
                if (job == _stationAiJob)
                {
                    var emptyAiCores = _emptyAiCores.TryGetValue(station, out var count) ? count : 0;
                    availableSlots = Math.Max(0, Math.Min(free, emptyAiCores) - CountIntellicardReservations(stationJobs));
                }

                if (!_stationJobs.TryGetJobSlot(station, job, out var slots, stationJobs)
                    || slots is not { } slot)
                    continue;

                var adjustment = availableSlots - slot;
                if (adjustment < 0 || job == _stationAiJob && allowAiSlotIncrease && adjustment > 0)
                    _stationJobs.TryAdjustJobSlot(station, job, adjustment, false, true, stationJobs);
            }
        }
    }

    private int CountIntellicardReservations(StationJobsComponent stationJobs)
    {
        var reservations = 0;
        foreach (var (userId, jobs) in stationJobs.PlayerJobs)
        {
            if (!jobs.Contains(_stationAiJob)
                || !_mind.TryGetMind(userId, out _, out var mind)
                || mind.OwnedEntity is not { } body
                || !_container.TryGetContainingContainer(body, out var container)
                || !HasComp<IntellicardComponent>(container.Owner))
                continue;

            reservations++;
        }

        return reservations;
    }
}
