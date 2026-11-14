// ReSharper disable CheckNamespace

using System.Numerics;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    public readonly record struct LandingAreaReservation(
        EntityUid MapUid,
        IReadOnlyList<Vector2i> NewlyModifiedTiles,
        IReadOnlyList<(Vector2i Index, Tile Tile)> GeneratedTiles);

    private const float LandingAreaClearance = 0.2f;

    private readonly HashSet<EntityUid> _landingAreaLookupEntities = [];

    public float GetLandingRadius(EntityUid shuttleUid)
    {
        var bounds = Comp<MapGridComponent>(shuttleUid).LocalAABB;
        return bounds.Center.Length() + (new Vector2(bounds.Width, bounds.Height).Length() / 2f);
    }

    public Box2 GetLandingAreaBounds(EntityUid shuttleUid, Vector2 gridOrigin, Angle rotation)
    {
        var bounds = Comp<MapGridComponent>(shuttleUid).LocalAABB;
        var bottomLeft = rotation.RotateVec(new Vector2(bounds.Left, bounds.Bottom));
        var bottomRight = rotation.RotateVec(new Vector2(bounds.Right, bounds.Bottom));
        var topLeft = rotation.RotateVec(new Vector2(bounds.Left, bounds.Top));
        var topRight = rotation.RotateVec(new Vector2(bounds.Right, bounds.Top));

        var min = Vector2.Min(Vector2.Min(bottomLeft, bottomRight), Vector2.Min(topLeft, topRight));
        var max = Vector2.Max(Vector2.Max(bottomLeft, bottomRight), Vector2.Max(topLeft, topRight));
        return new Box2(gridOrigin + min, gridOrigin + max).Enlarged(LandingAreaClearance);
    }

    public bool IsLandingAreaClear(
        EntityUid mapUid,
        MapGridComponent mapGrid,
        Box2 landingBounds,
        IReadOnlySet<Vector2i>? blockedTiles = null)
    {
        if (blockedTiles != null)
        {
            foreach (var tile in _mapManager.GetLocalTilesIntersecting(mapUid, mapGrid, landingBounds, false))
            {
                if (blockedTiles.Contains(tile.GridIndices))
                    return false;
            }
        }

        _landingAreaLookupEntities.Clear();
        _lookup.GetLocalEntitiesIntersecting(
            mapUid,
            landingBounds,
            _landingAreaLookupEntities,
            LookupFlags.Uncontained & ~LookupFlags.Sensors);

        foreach (var entity in _landingAreaLookupEntities)
        {
            if (!TryComp<FixturesComponent>(entity, out var fixtures))
                continue;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (fixture.Hard)
                    return false;
            }
        }

        return true;
    }

    public LandingAreaReservation ReserveLandingArea(EntityUid mapUid, MapGridComponent mapGrid, Box2 landingBounds)
    {
        var generatedTiles = new List<(Vector2i Index, Tile Tile)>();
        var newlyModifiedTiles = new List<Vector2i>();
        _biomes.ReserveLandingTiles(mapUid, landingBounds, generatedTiles, newlyModifiedTiles, mapGrid: mapGrid);
        return new LandingAreaReservation(mapUid, newlyModifiedTiles, generatedTiles);
    }

    public void ReleaseLandingArea(LandingAreaReservation reservation) =>
        _biomes.ReleaseLandingTiles(reservation.MapUid, reservation.NewlyModifiedTiles, reservation.GeneratedTiles);

    public bool CancelFTL(EntityUid shuttleUid)
    {
        if (!TryComp<FTLComponent>(shuttleUid, out var ftl))
            return false;

        _audio.Stop(ftl.StartupStream);
        _audio.Stop(ftl.TravelStream);
        RemComp<FTLComponent>(shuttleUid);
        _console.RefreshShuttleConsoles(shuttleUid);
        return true;
    }
}
