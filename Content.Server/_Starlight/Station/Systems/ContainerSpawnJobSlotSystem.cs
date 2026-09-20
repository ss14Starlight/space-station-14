using Content.Server.GameTicking.Events;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Station.Systems;

/// <summary>
/// Caps job slots that can only be filled through a container spawn point.
/// </summary>
public sealed partial class ContainerSpawnJobSlotSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _station = default!;

    private static readonly TimeSpan _refreshCooldown = TimeSpan.FromSeconds(5);

    private readonly Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int>> _freeContainers = new();

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
    public void RefreshJobSlots()
    {
        _nextTick = _timing.CurTime + _refreshCooldown;

        _freeContainers.Clear();

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
                if (!_stationJobs.TryGetJobSlot(station, job, out var slots, stationJobs)
                    || slots is not { } slot
                    || slot <= free)
                    continue;

                _stationJobs.TryAdjustJobSlot(station, job, free - slot, false, true, stationJobs);
            }
        }
    }
}
