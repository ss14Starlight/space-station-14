using Content.Server.Worldgen.Systems;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Starlight.GasMining.GasAsteroid;

public sealed partial class GasAsteroidFloorPlanPopulatorSystem : BaseWorldSystem
{
    [Dependency] SharedMapSystem _map = default!;
    [Dependency] IRobustRandom _random = default!;
    [Dependency] TurfSystem _turf = default!;

    [SubscribeLocalEvent]
    private void OnFloorPlanBuilt(EntityUid uid,
    GasAsteroidFloorPlanPopulatorComponent component,
    LocalStructureLoadedEvent args)
    {
        var placeables = new List<string?>(4);
        var grid = Comp<MapGridComponent>(uid);
        var enumerator = _map.GetAllTiles(uid, grid);

        while (enumerator.MoveNext(out var tile))
        {
            var coords = _map.GridTileToLocal(uid, grid, tile.Value.GridIndices);
            var selector = _turf.GetContentTileDefinition(tile.Value).ID;
            if (!component.Caches.TryGetValue(selector, out var cache))
                continue;

            placeables.Clear();
            cache.GetSpawns(_random, ref placeables);

            foreach (var proto in placeables)
            {
                if (proto is null)
                    continue;

                Spawn(proto, coords);
            }
        }

        var validTilePassEnumerator = _map.GetAllTiles(uid, grid);
        var numberOfNodes = _random.Next(component.MinNodes, component.MaxNodes);
        List<EntityCoordinates> validNodeTiles = new List<EntityCoordinates>();

        while (validTilePassEnumerator.MoveNext(out var tile))
        {
            var coords = _map.GridTileToLocal(uid, grid, tile.Value.GridIndices);
            if (_map.AnchoredEntityCount(uid, grid, tile.Value.GridIndices) == 0)
            {
                validNodeTiles.Add(coords);
            }
        }
        numberOfNodes = Math.Clamp(numberOfNodes, 0, validNodeTiles.Count);

        for (var i = 0; i < numberOfNodes; i++)
        {
            var index = _random.Next(validNodeTiles.Count);
            var tile = validNodeTiles[index];
            validNodeTiles.RemoveAt(index);

            Spawn("GasNode", tile);
        }
    }
}
