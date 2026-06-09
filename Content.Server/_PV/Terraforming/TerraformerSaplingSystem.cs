using Content.Shared._PV.Terraforming;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._PV.Terraforming;

public sealed class TerraformerSaplingSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // The main TerraformerSystem already subscribes to TerraformerComponent + InteractUsingEvent.
        // Subscribe through TransformComponent instead and filter for terraformers to avoid duplicate subscriptions.
        SubscribeLocalEvent<TransformComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, TransformComponent xformComp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<TerraformerComponent>(uid, out _))
            return;

        if (!TryComp<TerraformerSaplingComponent>(args.Used, out var sapling))
            return;

        var treePrototype = sapling.TreePrototype;
        var spawnDelay = sapling.SpawnDelay;

        QueueDel(args.Used);
        args.Handled = true;

        _popup.PopupEntity("You load the sapling into the terraformer.", uid, args.User);

        Timer.Spawn(TimeSpan.FromSeconds(spawnDelay), () => TrySpawnSaplingTree(uid, treePrototype));
    }

    private void TrySpawnSaplingTree(EntityUid uid, string treePrototype)
    {
        if (Deleted(uid))
            return;

        if (!TryComp<TerraformerComponent>(uid, out var terraformer))
            return;

        if (!TryComp<TransformComponent>(uid, out var xform))
            return;

        if (xform.GridUid == null)
            return;

        var gridUid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var validTiles = new List<TileRef>();

        foreach (var tile in GetTilesInRadius(gridUid, grid, xform.Coordinates, terraformer.Radius))
        {
            var tileDefinition = _tileDefinition[tile.Tile.TypeId];

            if (!terraformer.TreeSpawnTiles.Contains(tileDefinition.ID))
                continue;

            if (!IsTileFreeForTree(grid, tile.GridIndices))
                continue;

            validTiles.Add(tile);
        }

        if (validTiles.Count == 0)
            return;

        var selectedTile = _random.Pick(validTiles);
        var coords = _map.GridTileToLocal(gridUid, grid, selectedTile.GridIndices);

        Spawn(treePrototype, coords);
    }

    private bool IsTileFreeForTree(MapGridComponent grid, Vector2i tileIndices)
    {
        foreach (var anchored in grid.GetAnchoredEntities(tileIndices))
        {
            if (Deleted(anchored))
                continue;

            return false;
        }

        return true;
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
