// ReSharper disable CheckNamespace

using System.Numerics;
using Content.Server.Salvage.Expeditions;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.Salvage;

public sealed partial class SalvageSystem
{
    private const float SalvageLandingPadAreaMultiplier = 1.5f;
    private const float SalvageLandingGap = 18f;
    private const float SalvageLandingSearchLimit = 128f;

    private bool PlaceSalvageShuttleAtClearLanding(EntityUid shuttleUid, EntityUid mapUid)
    {
        if (!TryComp<SalvageExpeditionComponent>(mapUid, out var expedition))
            return false;

        if (!TryComp<FTLComponent>(shuttleUid, out var ftl) ||
            ftl.TargetCoordinates.EntityId != mapUid)
        {
            Log.Error($"Shuttle {ToPrettyString(shuttleUid)} made an FTL request for salvage map {ToPrettyString(mapUid)} without a matching FTL target.");
            return false;
        }

        if (!TryComp<MapGridComponent>(mapUid, out var mapGrid))
        {
            Log.Error($"Salvage map {ToPrettyString(mapUid)} has no grid for shuttle landing.");
            return false;
        }

        var landingAngle = Angle.Zero;
        if (!TryFindSalvageLanding(shuttleUid, mapUid, mapGrid, ftl, landingAngle, expedition.LandingBlockedTiles,
                out var landingCoordinates, out var dungeonCenter, out var landingCenter))
        {
            Log.Error($"Could not find a clear landing area for shuttle {ToPrettyString(shuttleUid)} on salvage map {ToPrettyString(mapUid)}.");
            return false;
        }

        ftl.TargetCoordinates = landingCoordinates;
        ftl.TargetAngle = landingAngle;
        Dirty(shuttleUid, ftl);
        expedition.DungeonLocation = dungeonCenter - landingCenter;
        return true;
    }

    private bool TryFindSalvageLanding(
        EntityUid shuttleUid,
        EntityUid mapUid,
        MapGridComponent mapGrid,
        FTLComponent ftl,
        Angle landingAngle,
        IReadOnlySet<Vector2i> dungeonTiles,
        out EntityCoordinates landingCoordinates,
        out Vector2 dungeonCenter,
        out Vector2 landingCenter)
    {
        landingCoordinates = default;
        dungeonCenter = default;
        landingCenter = default;

        if (dungeonTiles.Count == 0)
            return false;

        var shuttleLocalCenter = Comp<PhysicsComponent>(shuttleUid).LocalCenter;
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        foreach (var tile in dungeonTiles)
        {
            minX = Math.Min(minX, tile.X);
            minY = Math.Min(minY, tile.Y);
            maxX = Math.Max(maxX, tile.X);
            maxY = Math.Max(maxY, tile.Y);
        }

        var tileSize = mapGrid.TileSize;
        var dungeonBounds = new Box2(
            new Vector2(minX * tileSize, minY * tileSize),
            new Vector2((maxX + 1) * tileSize, (maxY + 1) * tileSize));
        var footprint = GetSalvageLandingBounds(shuttleUid, Vector2.Zero, landingAngle);
        var originalPosition = ftl.TargetCoordinates.Position;

        for (var extraDistance = 0f; extraDistance <= SalvageLandingSearchLimit; extraDistance += tileSize)
        {
            var gap = SalvageLandingGap + extraDistance;
            var candidates = new[]
            {
                new Vector2(dungeonBounds.Left - footprint.Right - gap, dungeonBounds.Center.Y - footprint.Center.Y),
                new Vector2(dungeonBounds.Right - footprint.Left + gap, dungeonBounds.Center.Y - footprint.Center.Y),
                new Vector2(dungeonBounds.Center.X - footprint.Center.X, dungeonBounds.Bottom - footprint.Top - gap),
                new Vector2(dungeonBounds.Center.X - footprint.Center.X, dungeonBounds.Top - footprint.Bottom + gap)
            };

            Array.Sort(candidates, (first, second) =>
                Vector2.DistanceSquared(first, originalPosition).CompareTo(Vector2.DistanceSquared(second, originalPosition)));

            foreach (var candidate in candidates)
            {
                var bounds = GetSalvageLandingBounds(shuttleUid, candidate, landingAngle);
                if (!_shuttle.IsLandingAreaClear(mapUid, mapGrid, bounds, dungeonTiles))
                    continue;

                var targetCenter = candidate + landingAngle.RotateVec(shuttleLocalCenter);
                var ftlCheckCoordinates = new EntityCoordinates(mapUid, targetCenter);
                if (!_shuttleConsoles.IsFTLFree(shuttleUid, ftlCheckCoordinates, landingAngle))
                    continue;

                _shuttle.ReserveLandingArea(mapUid, mapGrid, bounds);
                landingCoordinates = new EntityCoordinates(mapUid, candidate);
                dungeonCenter = dungeonBounds.Center;
                landingCenter = targetCenter;
                return true;
            }
        }

        return false;
    }

    private Box2 GetSalvageLandingBounds(EntityUid shuttleUid, Vector2 gridOrigin, Angle rotation)
    {
        var shuttleBounds = _shuttle.GetLandingAreaBounds(shuttleUid, gridOrigin, rotation);
        var halfExtents = new Vector2(shuttleBounds.Width, shuttleBounds.Height)
            * (MathF.Sqrt(SalvageLandingPadAreaMultiplier) / 2f);

        return new Box2(shuttleBounds.Center - halfExtents, shuttleBounds.Center + halfExtents);
    }
}
