using System.Numerics;
using System.Linq;
using Content.Shared._Starlight.EntityBeacon.Components;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityBeacon.EntitySystems;

public sealed class EntityBeaconSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly ITileDefinitionManager _tiledef = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EntityBeaconComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Range >= component.RangeLimit)
                continue;

            if (component.NextUpdateTime == TimeSpan.FromSeconds(0))
                component.NextUpdateTime = Timing.CurTime;

            if (Timing.CurTime < component.NextUpdateTime)
                continue;

            component.NextUpdateTime += component.Delay;

            var xform = Transform(uid);
            var centerCoords = xform.Coordinates;

            bool EntitiesEnough = component.CoordinatesToSpawn.Count > 6;
            if (!EntitiesEnough)
            {
                component.Range = Math.Min(component.RangeLimit, component.Range + 2);

                if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
                    return;

                var enumerator = new FreeSpaceEnumerator(_map, _turf, true, true, null, xform.GridUid.Value, grid, 
                    new Box2(centerCoords.Position + new Vector2(-component.Range, -component.Range), centerCoords.Position + new Vector2(component.Range, component.Range)), true);
                
                while (enumerator.MoveNext(out var coordinates))
                {
                    var entities = _lookup.GetEntitiesIntersecting(coordinates);
                    if (entities.Any(p => MetaData(p).EntityPrototype is { ID: var id } && component.EntitiesToSpawn.Contains((EntProtoId)id)))
                        continue;
                    component.CoordinatesToSpawn.Add(coordinates);
                }
            }

            if (component.CoordinatesToSpawn.Count > 6)
            {
                var coordinates = _random.Pick(component.CoordinatesToSpawn);
                var entity = _random.Pick(component.EntitiesToSpawn);
                component.CoordinatesToSpawn.Remove(coordinates);

                EntityManager.PredictedSpawnAtPosition(entity, coordinates);
            }
        }
    }
}

/// <summary>
/// Iterates the local tiles of the specified data.
/// </summary>
public struct FreeSpaceEnumerator
{
    private readonly SharedMapSystem _mapSystem;
    private readonly TurfSystem _turfSystem;

    private readonly EntityUid _uid; // GridUid
    private readonly MapGridComponent _grid;
    private readonly bool _ignoreFull;
    private readonly bool _ignoreEmpty;
    private readonly bool _filterMobs;
    private readonly Predicate<EntityCoordinates>? _predicate;

    private readonly int _lowerY;
    private readonly int _upperX;
    private readonly int _upperY;

    private int _x;
    private int _y;

    public FreeSpaceEnumerator(
        SharedMapSystem mapSystem,
        TurfSystem turfSystem,
        bool ignoreFull,
        bool ignoreEmpty,
        Predicate<EntityCoordinates>? predicate,
        EntityUid uid,
        MapGridComponent grid,
        Box2 aabb,
        bool filterMobs = false)
    {
        _mapSystem = mapSystem;
        _turfSystem = turfSystem;

        _uid = uid;
        _grid = grid;
        _ignoreFull = ignoreFull;
        _ignoreEmpty = ignoreEmpty;
        _filterMobs = filterMobs;
        _predicate = predicate;

        // TODO: Should move the intersecting calls onto mapmanager system and then allow people to pass in xform / xformquery
        // that way we can avoid the GetComp here.
        var gridTileLb = new Vector2i((int)Math.Floor(aabb.Left), (int)Math.Floor(aabb.Bottom));
        // If we have 20.1 we want to include that tile but if we have 20 then we don't.
        var gridTileRt = new Vector2i((int)Math.Ceiling(aabb.Right), (int)Math.Ceiling(aabb.Top));

        _x = gridTileLb.X;
        _y = gridTileLb.Y;
        _lowerY = gridTileLb.Y;
        _upperX = gridTileRt.X;
        _upperY = gridTileRt.Y;
    }

    public bool MoveNext(out EntityCoordinates coordinates)
    {
        while (true)
        {
            if (_x >= _upperX)
            {
                coordinates = new();
                return false;
            }

            var gridTile = new Vector2i(_x, _y);

            TileRef? tile = null;

            _y++;

            if (_y >= _upperY)
            {
                _x++;
                _y = _lowerY;
            }

            var gridChunk = _mapSystem.GridTileToChunkIndices(_uid, _grid, gridTile);

            coordinates = new EntityCoordinates(_uid, _x, _y);
            tile = _mapSystem.GetTileRef(_uid, _grid, coordinates);

            if (tile is not { } tileRef)
                continue;

            if (_ignoreFull && _turfSystem.IsTileBlocked(tileRef, _filterMobs ? CollisionGroup.MobMask : CollisionGroup.Impassable))
                continue;

            if (_ignoreEmpty && tileRef.Tile.IsEmpty)
                continue;

            if (_predicate == null || _predicate(coordinates))
                return true;
        }
    }
}