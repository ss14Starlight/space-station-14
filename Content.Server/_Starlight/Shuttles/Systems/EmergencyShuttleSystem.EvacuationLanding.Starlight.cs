using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Shuttles.Components;
using Content.Shared._Starlight.Computers.PodConsole;
using Content.Shared.Procedural;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

// ReSharper disable CheckNamespace
namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem
{
    private const int EvacuationLandingSearchAttemptsPerRing = 32;
    private const int EvacuationLandingSearchRings = 8;
    private const float EvacuationPodLandingGap = 2f;

    private readonly record struct EvacuationPodLandingReservation(
        Vector2 Position,
        float Radius,
        ShuttleSystem.LandingAreaReservation LandingArea);

    private readonly List<(Vector2 Position, float Radius)> _evacuationPodLandingReservations = [];
    private readonly HashSet<Vector2i> _evacuationDungeonTiles = [];
    private readonly List<(string ConfigId, Task<List<Dungeon>> Generation)> _evacuationDungeonGenerationTasks = [];
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
                else
                {
                    foreach (var dungeon in generation.GetAwaiter().GetResult())
                    {
                        _evacuationDungeonTiles.UnionWith(dungeon.AllTiles);
                    }
                }
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

    private EntityCoordinates? GetEvacuationPodLandingCoordinates(
        EntityUid shuttleUid,
        EntityCoordinates landingZone,
        Angle rotation,
        out EvacuationPodLandingReservation reservation)
    {
        reservation = default;

        if (_evacuationPodLandingMap != landingZone.EntityId)
        {
            _evacuationPodLandingMap = landingZone.EntityId;
            _evacuationPodLandingReservations.Clear();
        }

        var mapGrid = Comp<MapGridComponent>(landingZone.EntityId);
        var radius = _shuttle.GetLandingRadius(shuttleUid);

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
                var landingBounds = _shuttle.GetLandingAreaBounds(shuttleUid, landingPosition, rotation);
                if (!_shuttle.IsLandingAreaClear(landingZone.EntityId, mapGrid, landingBounds, _evacuationDungeonTiles))
                    continue;

                var landingArea = _shuttle.ReserveLandingArea(landingZone.EntityId, mapGrid, landingBounds);
                reservation = new EvacuationPodLandingReservation(offset, radius, landingArea);
                _evacuationPodLandingReservations.Add((offset, radius));
                return landingZone.Offset(offset);
            }
        }

        return null;
    }
}
