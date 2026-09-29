using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Spawners.Components;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed partial class ContainerSpawnPointSystem : EntitySystem
{
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawningEvent>(HandlePlayerSpawning, before: new []{ typeof(SpawnPointSystem) });
    }

    //starlight priority spawning
    private bool IsJobAllowed(ContainerSpawnPointComponent spawnPoint, ProtoId<JobPrototype>? job)
    {
        if (job == null)
            return false;

        if (spawnPoint.Job != null)
            return spawnPoint.Job == job;

        if (spawnPoint.Department != null &&
            _proto.Resolve(spawnPoint.Department.Value, out var departmentProto))
        {
            return departmentProto.Roles.Contains(job.Value);
        }
            return true;
    }

    private int GetSpawnPriority(ContainerSpawnPointComponent spawnPoint)
    {
        if (spawnPoint.Job != null)
            return 2;

        if (spawnPoint.Department != null)
            return 1;

        return 0;
    }
    //starlight end

    public void HandlePlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        // If it's just a spawn pref check if it's for cryo (silly).
        if (args.HumanoidCharacterProfile?.SpawnPriority != SpawnPriorityPreference.Cryosleep &&
            (!_proto.Resolve(args.Job, out var jobProto) || jobProto.JobEntity == null))
        {
            //starlight
            //check if we should be allowed to skip by seeing if there is a normal spawn point available
            //ripped from normal spawn point system code (lazy I know but I cant be arsed)
            var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
            {
                if (args.Station != null && _station.GetOwningStation(uid, xform) != args.Station)
                    continue;

                if (_gameTicker.RunLevel == GameRunLevel.InRound && spawnPoint.SpawnType == SpawnPointType.LateJoin)
                {
                    return;
                }

                if (_gameTicker.RunLevel != GameRunLevel.InRound &&
                    spawnPoint.SpawnType == SpawnPointType.Job &&
                    (args.Job == null || spawnPoint.Job == null || spawnPoint.Job == args.Job))
                {
                    return;
                }
            }
            //starlight end
        }

        var query = EntityQueryEnumerator<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>();
        var possibleContainers = new List<Entity<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>>();
        //starlight
        var spawnPriority = -1;

        while (query.MoveNext(out var uid, out var spawnPoint, out var container, out var xform))
        {
            if (args.Station != null && _station.GetOwningStation(uid, xform) != args.Station)
                continue;

            if (spawnPoint.SpawnType != SpawnPointType.Unset &&
                (_gameTicker.RunLevel == GameRunLevel.InRound
                    ? spawnPoint.SpawnType != SpawnPointType.LateJoin
                    : spawnPoint.SpawnType != SpawnPointType.Job))
                continue;

            if (!IsJobAllowed(spawnPoint, args.Job))
                continue;

            var priority = GetSpawnPriority(spawnPoint);

            if (priority > spawnPriority)
            {
                possibleContainers.Clear();
                spawnPriority = priority;
            }

            if (priority == spawnPriority)
                possibleContainers.Add((uid, spawnPoint, container, xform));
        }
        //starlight end

        if (possibleContainers.Count == 0)
            return;

        // we just need some default coords so we can spawn the player entity.
        var baseCoords = possibleContainers[0].Comp3.Coordinates;

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            baseCoords,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);

        _random.Shuffle(possibleContainers);
        foreach (var (uid, spawnPoint, manager, xform) in possibleContainers)
        {
            if (!_container.TryGetContainer(uid, spawnPoint.ContainerId, out var container, manager))
                continue;

            if (!_container.Insert(args.SpawnResult.Value, container, containerXform: xform))
                continue;

            var ev = new ContainerSpawnEvent(args.SpawnResult.Value);
            RaiseLocalEvent(uid, ref ev);

            return;
        }

        Del(args.SpawnResult);
        args.SpawnResult = null;
    }
}
//starlight end

/// <summary>
/// Raised on a container when a player is spawned into it.
/// </summary>
[ByRefEvent]
public record struct ContainerSpawnEvent(EntityUid Player);
