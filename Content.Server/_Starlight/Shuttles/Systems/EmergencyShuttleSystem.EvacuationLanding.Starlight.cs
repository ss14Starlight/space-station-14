using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

// ReSharper disable CheckNamespace
namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem
{
    private readonly List<(Vector2 Position, float Radius)> _evacuationPodLandingReservations = [];
    private EntityUid? _evacuationPodLandingMap;

    private EntityCoordinates GetEvacuationPodLandingCoordinates(EntityUid shuttleUid, EntityCoordinates landingZone)
    {
        if (_evacuationPodLandingMap != landingZone.EntityId)
        {
            _evacuationPodLandingMap = landingZone.EntityId;
            _evacuationPodLandingReservations.Clear();
        }

        var bounds = Comp<MapGridComponent>(shuttleUid).LocalAABB;
        var radius = bounds.Center.Length() + (new Vector2(bounds.Width, bounds.Height).Length() / 2f);

        for (var attempt = 0; attempt < 32; attempt++)
        {
            var distance = MathF.Sqrt(_random.NextFloat()) * PodSpreadRadius;
            var offset = _random.NextAngle().ToVec() * distance;
            var clear = true;

            foreach (var (reservedPosition, reservedRadius) in _evacuationPodLandingReservations)
            {
                var minimumDistance = radius + reservedRadius + 2f;
                if (!(Vector2.DistanceSquared(offset, reservedPosition) < minimumDistance * minimumDistance))
                    continue;
                clear = false;
                break;
            }

            if (!clear)
                continue;

            _evacuationPodLandingReservations.Add((offset, radius));
            return landingZone.Offset(offset);
        }

        Log.Warning($"No collision-free evacuation pod landing point found within {PodSpreadRadius} units for {ToPrettyString(shuttleUid)}; expanding the landing spread.");

        // Place just beyond all reserved landing clearances when the preferred spread is full.
        var fallbackDistance = radius + 2f;
        foreach (var (reservedPosition, reservedRadius) in _evacuationPodLandingReservations)
        {
            fallbackDistance = MathF.Max(fallbackDistance, reservedPosition.Length() + reservedRadius + radius + 2f);
        }

        var fallbackOffset = _random.NextAngle().ToVec() * fallbackDistance;
        _evacuationPodLandingReservations.Add((fallbackOffset, radius));
        return landingZone.Offset(fallbackOffset);
    }
}
