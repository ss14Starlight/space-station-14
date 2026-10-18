using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Shuttles.Components;
using Content.Shared._Starlight.Computers.PodConsole;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

// ReSharper disable CheckNamespace
namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem
{
    private const int EvacuationLandingSearchAttemptsPerRing = 32;
    private const int EvacuationLandingSearchRings = 8;
    private const float EvacuationPodLandingGap = 2f;

    [Dependency] private EntityLookupSystem _lookup = default!;

    private readonly List<(Vector2 Position, float Radius)> _evacuationPodLandingReservations = [];
    private readonly HashSet<EntityUid> _evacuationLandingLookupEntities = [];
    private readonly List<(string ConfigId, Task Generation)> _evacuationDungeonGenerationTasks = [];
    private readonly List<(EntityUid Uid, float TravelTime)> _pendingEvacuationPodLaunches = [];
    private EntityUid? _evacuationPodLandingMap;
    private bool _evacuationPlanetGenerated;

    public int UnlockAllEscapePods()
    {
        var unlocked = 0;
        var podConsoleQuery = EntityQueryEnumerator<PodConsoleComponent>();

        while (podConsoleQuery.MoveNext(out var uid, out var podConsole))
        {
            if (!podConsole.Locked)
                continue;

            podConsole.Locked = false;
            Dirty(uid, podConsole);
            unlocked++;
        }

        return unlocked;
    }

    public int LaunchAllEscapePods()
    {
        var pods = new List<(EntityUid Uid, ShuttleComponent Shuttle)>();
        var podLaunchQuery = EntityQueryEnumerator<EscapePodComponent, ShuttleComponent>();

        while (podLaunchQuery.MoveNext(out var uid, out var pod, out var shuttle))
        {
            pod.LaunchTime = null;
            pods.Add((uid, shuttle));
        }

        foreach (var (uid, shuttle) in pods)
        {
            var podConsoleQuery = EntityQueryEnumerator<PodConsoleComponent>();
            while (podConsoleQuery.MoveNext(out var consoleUid, out var podConsole))
            {
                if (Transform(consoleUid).ParentUid != uid)
                    continue;

                podConsole.LaunchTime = null;
                RemCompDeferred<PodConsoleComponent>(consoleUid);
            }

            RemComp<EscapePodComponent>(uid);
            LaunchEscapePod(uid, shuttle, 10f);
        }

        return pods.Count;
    }

    private void EnsureEvacuationPlanet()
    {
        if (_evacuationPlanetMap is { } mapUid &&
            Exists(mapUid) &&
            _evacuationLandingZone != null)
            return;

        _evacuationPlanetMap = null;
        _evacuationLandingZone = null;
        _evacuationPlanetGenerated = false;
        SetupEvacuationPlanet();
    }

    private void UpdateEvacuationPlanetGeneration()
    {
        if (!_evacuationPlanetGenerated)
        {
            if (_evacuationDungeonGenerationTasks.Count == 0 ||
                _evacuationDungeonGenerationTasks.Exists(job => !job.Generation.IsCompleted))
                return;

            foreach (var (configId, generation) in _evacuationDungeonGenerationTasks)
            {
                if (generation.IsFaulted)
                    Log.Warning($"Failed to generate evacuation planet dungeon {configId}: {generation.Exception?.GetBaseException().Message}");
                else if (generation.IsCanceled)
                    Log.Warning($"Generation of evacuation planet dungeon {configId} was cancelled.");
            }

            _evacuationDungeonGenerationTasks.Clear();
            _evacuationPlanetGenerated = true;
            Log.Info("Evacuation planet generation finished; searching for landing sites is now safe.");
        }

        while (_pendingEvacuationPodLaunches.Count > 0)
        {
            var (uid, travelTime) = _pendingEvacuationPodLaunches[0];
            _pendingEvacuationPodLaunches.RemoveAt(0);

            if (!TryComp<ShuttleComponent>(uid, out var shuttle))
                continue;

            LaunchEscapePod(uid, shuttle, travelTime);
        }
    }

    private EntityCoordinates? GetEvacuationPodLandingCoordinates(EntityUid shuttleUid, EntityCoordinates landingZone)
    {
        if (_evacuationPodLandingMap != landingZone.EntityId)
        {
            _evacuationPodLandingMap = landingZone.EntityId;
            _evacuationPodLandingReservations.Clear();
        }

        var bounds = Comp<MapGridComponent>(shuttleUid).LocalAABB;
        var radius = bounds.Center.Length() + (new Vector2(bounds.Width, bounds.Height).Length() / 2f);
        var halfExtents = new Vector2(radius);

        for (var ring = 0; ring < EvacuationLandingSearchRings; ring++)
        {
            var innerDistance = ring * PodSpreadRadius;
            var outerDistance = (ring + 1) * PodSpreadRadius;
            var innerDistanceSquared = innerDistance * innerDistance;
            var distanceRangeSquared = (outerDistance * outerDistance) - innerDistanceSquared;

            for (var attempt = 0; attempt < EvacuationLandingSearchAttemptsPerRing; attempt++)
            {
                var distance = MathF.Sqrt(innerDistanceSquared + (_random.NextFloat() * distanceRangeSquared));
                var offset = _random.NextAngle().ToVec() * distance;
                var reserved = false;

                foreach (var (reservedPosition, reservedRadius) in _evacuationPodLandingReservations)
                {
                    var minimumDistance = radius + reservedRadius + EvacuationPodLandingGap;
                    if (Vector2.DistanceSquared(offset, reservedPosition) >= minimumDistance * minimumDistance)
                        continue;

                    reserved = true;
                    break;
                }

                if (reserved)
                    continue;

                var landingPosition = landingZone.Position + offset;
                var landingBounds = new Box2(landingPosition - halfExtents, landingPosition + halfExtents).Enlarged(0.2f);
                if (!IsEvacuationLandingAreaClear(landingZone.EntityId, landingBounds))
                    continue;

                _evacuationPodLandingReservations.Add((offset, radius));

                var tiles = new List<(Vector2i Index, Tile Tile)>();
                _biomes.ReserveTiles(landingZone.EntityId, landingBounds, tiles);
                return landingZone.Offset(offset);
            }
        }

        return null;
    }

    private bool IsEvacuationLandingAreaClear(EntityUid mapUid, Box2 landingBounds)
    {
        _evacuationLandingLookupEntities.Clear();
        _lookup.GetLocalEntitiesIntersecting(
            mapUid,
            landingBounds,
            _evacuationLandingLookupEntities,
            LookupFlags.Uncontained & ~LookupFlags.Sensors);

        foreach (var entity in _evacuationLandingLookupEntities)
        {
            if (!TryComp<FixturesComponent>(entity, out var fixtures))
                continue;

            foreach (var fixture in fixtures.Fixtures.Values)
                if (fixture.Hard)
                    return false;
        }

        return true;
    }
}
