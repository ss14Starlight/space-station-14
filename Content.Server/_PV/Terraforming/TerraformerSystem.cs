using Content.Shared._PV.Terraforming;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._PV.Terraforming;

public sealed class TerraformerSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TerraformerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var terraformer, out var xform))
        {
            if (!terraformer.Active)
                continue;

            if (terraformer.Fuel <= 0)
            {
                terraformer.Active = false;
                Dirty(uid, terraformer);
                continue;
            }

            terraformer.Fuel -= terraformer.FuelPerSecond * frameTime;
            terraformer.Accumulator += frameTime;

            if (terraformer.Accumulator < terraformer.TileConvertCooldown)
                continue;

            terraformer.Accumulator = 0f;

            TryTerraformOneTile(terraformer, xform);

            Dirty(uid, terraformer);
        }
    }

    private void TryTerraformOneTile(TerraformerComponent terraformer, TransformComponent xform)
    {
        if (xform.GridUid == null)
            return;

        var gridUid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var validTiles = new List<TileRef>();

        foreach (var tile in GetTilesInRadius(gridUid, grid, xform.Coordinates, terraformer.Radius))
        {
            var tileDefinition = _tileDefinition[tile.Tile.TypeId];

            if (!terraformer.SourceTiles.Contains(tileDefinition.ID))
                continue;

            validTiles.Add(tile);
        }

        if (validTiles.Count == 0)
            return;

        var selectedTile = _random.Pick(validTiles);
        var targetTile = _tileDefinition[terraformer.TargetTile];

        _map.SetTile(gridUid, grid, selectedTile.GridIndices, new Tile(targetTile.TileId));
    }

    private IEnumerable<TileRef> GetTilesInRadius(
        EntityUid gridUid,
        MapGridComponent grid,
        EntityCoordinates center,
        float radius)
    {
        var centerTile = _map.GetTileRef(gridUid, grid, center);
        var radiusInt = (int) MathF.Ceiling(radius);

        for (var x = -radiusInt; x <= radiusInt; x++)
        {
            for (var y = -radiusInt; y <= radiusInt; y++)
            {
                if (x * x + y * y > radius * radius)
                    continue;

                var tileIndices = centerTile.GridIndices + new Vector2i(x, y);

                if (!_map.TryGetTileRef(gridUid, grid, tileIndices, out var tile))
                    continue;

                yield return tile;
            }
        }
    }
}