using Content.Server.Worldgen.Systems;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Starlight.GasMining.GasAsteroid;

public sealed partial class GasAsteroidFloorPlanPopulatorSystem : BaseWorldSystem
{
    [Dependency] SharedMapSystem _map = default!;
    [Dependency] IRobustRandom _random = default!;
    [Dependency] TurfSystem _turf = default!;

    [SubscribeLocalEvent]
    private void OnFloorPlanBuilt(Entity<GasAsteroidFloorPlanPopulatorComponent> entity, ref LocalStructureLoadedEvent args)
    {
        var placeables = new List<string?>(4);
        var grid = Comp<MapGridComponent>(entity.Owner);
        var enumerator = _map.GetAllTiles(entity, grid);

        var numberOfNodes = _random.Next(entity.Comp.MinNodes, entity.Comp.MaxNodes);

        while (enumerator.MoveNext(out var tile))
        {
            var coords = _map.GridTileToLocal(entity, grid, tile.Value.GridIndices);
            var selector = _turf.GetContentTileDefinition(tile.Value).ID;
        }
    }
}
