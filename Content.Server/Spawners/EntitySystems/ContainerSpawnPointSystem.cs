using System.Linq;
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

    public void HandlePlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        // If it's just a spawn pref check if it's for cryo (silly).
        if (args.HumanoidCharacterProfile?.SpawnPriority != SpawnPriorityPreference.Cryosleep &&
            (!_proto.Resolve(args.Job, out var jobProto) || jobProto.JobEntity == null))
        {
            //Starlight-Start
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
        }

        var query = EntityQueryEnumerator<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>();
        var possibleContainers = new List<Entity<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>>();

        while (query.MoveNext(out var uid, out var spawnPoint, out var container, out var xform))
        {
            if (spawnPoint.SpawnType != SpawnPointType.Unset &&
                (_gameTicker.RunLevel == GameRunLevel.InRound
                    ? spawnPoint.SpawnType != SpawnPointType.LateJoin
                    : spawnPoint.SpawnType != SpawnPointType.Job))
                continue;

            if (!IsJobAllowed(spawnPoint, args.Job))
                continue;

            possibleContainers.Add((uid, spawnPoint, container, xform));
        }

        possibleContainers = possibleContainers
            .GroupBy(x => GetSpawnPriority(x.Comp1))
            .OrderByDescending(x => x.Key)
            .SelectMany(x =>
            {
                var group = x.ToList();
                _random.Shuffle(group);
                return group;
            })
            .ToList();
        if (possibleContainers.Count == 0)
            return;

        foreach (var (uid, spawnPoint, manager, xform) in possibleContainers)
        {
            if (!_container.TryGetContainer(uid, spawnPoint.ContainerId, out var container, manager))
                continue;

            if (container.ContainedEntities.Count > 0)
                continue;

            var spawnResult = _stationSpawning.SpawnPlayerMob(
                xform.Coordinates,
                args.Job,
                args.HumanoidCharacterProfile,
                args.Station);

            if (!_container.Insert(spawnResult, container, containerXform: xform))
            {
                Del(spawnResult);
                continue;
            }

            args.SpawnResult = spawnResult;

            var ev = new ContainerSpawnEvent(spawnResult);
            RaiseLocalEvent(uid, ref ev);

            return;
        }
        // Starlight-end
    }
}

/// <summary>
/// Raised on a container when a player is spawned into it.
/// </summary>
[ByRefEvent]
public record struct ContainerSpawnEvent(EntityUid Player);
