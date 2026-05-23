using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Research.Systems;
using Content.Shared._PV.Terraforming;
using Content.Shared.Atmos;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._PV.Terraforming;

public sealed class TerraformerSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly ResearchSystem _research = default!;

    private const string TerraformerBarrierPrototype = "TerraformerAtmosBarrier";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerraformerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TerraformerComponent, ComponentShutdown>(OnTerraformerShutdown);
    }

    private void OnTerraformerShutdown(EntityUid uid, TerraformerComponent comp, ComponentShutdown args)
    {
        DeleteBarriers(comp);
        ForceAllOtherTerraformersToRefresh(uid);
    }

    private void OnInteractUsing(EntityUid uid, TerraformerComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<StackComponent>(args.Used, out var stack))
            return;

        if (stack.StackTypeId != comp.BiomassStack)
            return;

        if (comp.Fuel >= comp.MaxFuel)
        {
            _popup.PopupEntity("The terraformer is already full.", uid, args.User);
            args.Handled = true;
            return;
        }

        var availableSpace = comp.MaxFuel - comp.Fuel;
        var biomassNeeded = (int) MathF.Ceiling(availableSpace / comp.FuelPerBiomass);

        if (biomassNeeded <= 0)
            return;

        var biomassAvailable = _stack.GetCount((args.Used, stack));
        var biomassToUse = Math.Min(biomassNeeded, biomassAvailable);

        if (biomassToUse <= 0)
            return;

        if (!_stack.TryUse((args.Used, stack), biomassToUse))
            return;

        comp.Fuel = MathF.Min(comp.MaxFuel, comp.Fuel + biomassToUse * comp.FuelPerBiomass);

        _popup.PopupEntity($"You load biomass into the terraformer. Fuel: {comp.Fuel:0}/{comp.MaxFuel:0}", uid, args.User);

        Dirty(uid, comp);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var activeBarrierTerraformersByGrid = new Dictionary<EntityUid, List<(EntityUid Uid, TerraformerComponent Terraformer, TransformComponent Xform)>>();
        var gridsNeedingBarrierRefresh = new HashSet<EntityUid>();

        var query = EntityQueryEnumerator<TerraformerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var terraformer, out var xform))
        {
            var poweredAndActive = terraformer.Active && IsPowered(uid);

            if (!poweredAndActive)
            {
                var deletedAny = DeleteBarriers(terraformer);

                if (deletedAny)
                    ForceAllOtherTerraformersToRefresh(uid);

                continue;
            }

            TrackBarrierTerraformer(uid, terraformer, xform, frameTime, activeBarrierTerraformersByGrid, gridsNeedingBarrierRefresh);

            if (terraformer.Fuel <= 0)
                continue;

            terraformer.Fuel -= terraformer.FuelPerSecond * frameTime;

            if (terraformer.Fuel <= 0)
            {
                terraformer.Fuel = 0;
                Dirty(uid, terraformer);
                continue;
            }

            terraformer.Accumulator += frameTime;
            terraformer.AtmosAccumulator += frameTime;
            terraformer.ScrubAccumulator += frameTime;

            if (terraformer.Accumulator >= terraformer.TileConvertCooldown)
            {
                terraformer.Accumulator = 0f;

                if (TryTerraformOneTile(terraformer, xform))
                {
                    terraformer.TilesTerraformed++;
                    AwardSciencePoints(uid, terraformer);
                }
            }

            if (terraformer.AtmosAccumulator >= terraformer.AtmosCooldown)
            {
                terraformer.AtmosAccumulator = 0f;
                TryGenerateAtmosphere(terraformer, xform);
            }

            if (terraformer.ScrubAccumulator >= terraformer.ScrubCooldown)
            {
                terraformer.ScrubAccumulator = 0f;
                TryScrubGases(terraformer, xform);
            }

            if (terraformer.SpawnTrees)
            {
                terraformer.TreeSpawnAccumulator += frameTime;

                if (terraformer.TreeSpawnAccumulator >= terraformer.TreeSpawnCooldown)
                {
                    terraformer.TreeSpawnAccumulator = 0f;
                    TrySpawnTree(uid, terraformer, xform);
                }
            }

            Dirty(uid, terraformer);
        }

        RepairDirtyBarrierNetworks(activeBarrierTerraformersByGrid, gridsNeedingBarrierRefresh);
    }

    private void TrackBarrierTerraformer(
        EntityUid uid,
        TerraformerComponent terraformer,
        TransformComponent xform,
        float frameTime,
        Dictionary<EntityUid, List<(EntityUid Uid, TerraformerComponent Terraformer, TransformComponent Xform)>> activeBarrierTerraformersByGrid,
        HashSet<EntityUid> gridsNeedingBarrierRefresh)
    {
        if (!terraformer.CreateBarriers)
        {
            var deletedAny = DeleteBarriers(terraformer);

            if (deletedAny)
                ForceAllOtherTerraformersToRefresh(uid);

            return;
        }

        if (xform.GridUid == null)
            return;

        var gridUid = xform.GridUid.Value;

        if (!activeBarrierTerraformersByGrid.TryGetValue(gridUid, out var list))
        {
            list = new List<(EntityUid, TerraformerComponent, TransformComponent)>();
            activeBarrierTerraformersByGrid[gridUid] = list;
        }

        list.Add((uid, terraformer, xform));

        terraformer.BarrierRefreshAccumulator += frameTime;

        var shouldRefresh =
            terraformer.ForceBarrierRefresh ||
            terraformer.SpawnedBarriers.Count == 0 ||
            terraformer.BarrierRefreshAccumulator >= terraformer.BarrierRefreshCooldown;

        if (shouldRefresh)
            gridsNeedingBarrierRefresh.Add(gridUid);
    }

    private void RepairDirtyBarrierNetworks(
        Dictionary<EntityUid, List<(EntityUid Uid, TerraformerComponent Terraformer, TransformComponent Xform)>> activeBarrierTerraformersByGrid,
        HashSet<EntityUid> gridsNeedingBarrierRefresh)
    {
        foreach (var gridUid in gridsNeedingBarrierRefresh)
        {
            if (!activeBarrierTerraformersByGrid.TryGetValue(gridUid, out var terraformers))
                continue;

            if (terraformers.Count == 0)
                continue;

            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            RepairBarrierNetworkForGrid(gridUid, grid, terraformers);

            foreach (var (uid, terraformer, _) in terraformers)
            {
                terraformer.ForceBarrierRefresh = false;
                terraformer.BarrierRefreshAccumulator = 0f;
                Dirty(uid, terraformer);
            }
        }
    }

    private void RepairBarrierNetworkForGrid(
        EntityUid gridUid,
        MapGridComponent grid,
        List<(EntityUid Uid, TerraformerComponent Terraformer, TransformComponent Xform)> terraformers)
    {
        var combinedArea = new HashSet<Vector2i>();

        foreach (var (_, terraformer, xform) in terraformers)
        {
            var barrierRadius = GetBarrierRadius(terraformer);

            foreach (var tile in GetReachableTilesInRadius(gridUid, grid, xform.Coordinates, barrierRadius))
            {
                combinedArea.Add(tile);
            }
        }

        if (combinedArea.Count == 0)
        {
            DeleteManagedBarriersOnGrid(gridUid);
            ClearTrackedBarriers(terraformers);
            return;
        }

        var desiredBoundaryTiles = GetBoundaryTiles(gridUid, grid, combinedArea);
        var existingBoundaryBarriers = RemoveInvalidAndDuplicateManagedBarriers(gridUid, grid, combinedArea, desiredBoundaryTiles);

        ClearTrackedBarriers(terraformers);

        var owner = terraformers[0].Terraformer;
        var barrierPrototype = owner.BarrierPrototype;

        foreach (var tile in desiredBoundaryTiles)
        {
            if (existingBoundaryBarriers.TryGetValue(tile, out var existing))
            {
                owner.SpawnedBarriers.Add(existing);
                continue;
            }

            var coords = _map.GridTileToLocal(gridUid, grid, tile);
            var barrier = Spawn(barrierPrototype, coords);

            EnsureComp<TerraformerBarrierComponent>(barrier);

            owner.SpawnedBarriers.Add(barrier);
        }
    }

    private Dictionary<Vector2i, EntityUid> RemoveInvalidAndDuplicateManagedBarriers(
        EntityUid gridUid,
        MapGridComponent grid,
        HashSet<Vector2i> combinedArea,
        HashSet<Vector2i> desiredBoundaryTiles)
    {
        var existingBoundaryBarriers = new Dictionary<Vector2i, EntityUid>();
        var barriersToDelete = new List<EntityUid>();

        var query = EntityQueryEnumerator<TerraformerBarrierComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            var tile = _map.GetTileRef(gridUid, grid, xform.Coordinates).GridIndices;

            // Remove barriers that ended up inside the terraform area but are no longer part of the outside outline.
            if (combinedArea.Contains(tile) && !desiredBoundaryTiles.Contains(tile))
            {
                barriersToDelete.Add(uid);
                continue;
            }

            // Remove barriers that are no longer on the desired outline.
            if (!desiredBoundaryTiles.Contains(tile))
            {
                barriersToDelete.Add(uid);
                continue;
            }

            // Keep only one barrier per outline tile.
            if (existingBoundaryBarriers.ContainsKey(tile))
            {
                barriersToDelete.Add(uid);
                continue;
            }

            existingBoundaryBarriers[tile] = uid;
        }

        foreach (var barrier in barriersToDelete)
        {
            QueueDel(barrier);
        }

        return existingBoundaryBarriers;
    }

    private void DeleteManagedBarriersOnGrid(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<TerraformerBarrierComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            QueueDel(uid);
        }
    }

    private void ClearTrackedBarriers(List<(EntityUid Uid, TerraformerComponent Terraformer, TransformComponent Xform)> terraformers)
    {
        foreach (var (_, terraformer, _) in terraformers)
        {
            terraformer.SpawnedBarriers.Clear();
        }
    }

    private HashSet<Vector2i> GetBoundaryTiles(EntityUid gridUid, MapGridComponent grid, HashSet<Vector2i> combinedArea)
    {
        var boundaryTiles = new HashSet<Vector2i>();

        foreach (var tile in combinedArea)
        {
            var isBoundary = false;

            foreach (var direction in CardinalDirections())
            {
                var neighbor = tile + direction;

                if (!combinedArea.Contains(neighbor))
                {
                    isBoundary = true;
                    break;
                }

                if (!TryGetUsableTile(gridUid, grid, neighbor, out _))
                {
                    isBoundary = true;
                    break;
                }

                if (IsTileBlockedByWall(grid, neighbor))
                {
                    isBoundary = true;
                    break;
                }
            }

            if (isBoundary)
                boundaryTiles.Add(tile);
        }

        return boundaryTiles;
    }

    private IEnumerable<Vector2i> GetReachableTilesInRadius(
        EntityUid gridUid,
        MapGridComponent grid,
        EntityCoordinates center,
        float radius)
    {
        var centerTile = _map.GetTileRef(gridUid, grid, center);
        var centerIndices = centerTile.GridIndices;

        if (!TryGetUsableTile(gridUid, grid, centerIndices, out _))
            yield break;

        if (IsTileBlockedByWall(grid, centerIndices))
            yield break;

        var reachable = new HashSet<Vector2i>();
        var queue = new Queue<Vector2i>();

        reachable.Add(centerIndices);
        queue.Enqueue(centerIndices);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var direction in CardinalDirections())
            {
                var neighbor = current + direction;

                if (reachable.Contains(neighbor))
                    continue;

                if (!IsTileInsideRadius(neighbor, centerIndices, radius))
                    continue;

                if (!TryGetUsableTile(gridUid, grid, neighbor, out _))
                    continue;

                if (IsTileBlockedByWall(grid, neighbor))
                    continue;

                reachable.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        foreach (var tile in reachable)
        {
            yield return tile;
        }
    }

    private IEnumerable<Vector2i> CardinalDirections()
    {
        yield return new Vector2i(1, 0);
        yield return new Vector2i(-1, 0);
        yield return new Vector2i(0, 1);
        yield return new Vector2i(0, -1);
    }

    private bool IsPowered(EntityUid uid)
    {
        if (!TryComp<ApcPowerReceiverComponent>(uid, out var power))
            return true;

        return power.Powered;
    }

    private void AwardSciencePoints(EntityUid terraformerUid, TerraformerComponent terraformer)
    {
        if (terraformer.SciencePointsPerTile <= 0)
            return;

        var query = EntityQueryEnumerator<ResearchClientComponent>();

        while (query.MoveNext(out var consoleUid, out var researchClient))
        {
            if (!IsPowered(consoleUid))
                continue;

            if (researchClient.Server == null)
                continue;

            _research.ModifyServerPoints(researchClient.Server.Value, terraformer.SciencePointsPerTile);
            return;
        }
    }

    private void ForceAllOtherTerraformersToRefresh(EntityUid removedUid)
    {
        var query = EntityQueryEnumerator<TerraformerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var terraformer, out _))
        {
            if (uid == removedUid)
                continue;

            if (!terraformer.Active)
                continue;

            if (!IsPowered(uid))
                continue;

            if (!terraformer.CreateBarriers)
                continue;

            terraformer.ForceBarrierRefresh = true;
            terraformer.BarrierRefreshAccumulator = terraformer.BarrierRefreshCooldown;

            Dirty(uid, terraformer);
        }
    }

    private float GetBarrierRadius(TerraformerComponent terraformer)
    {
        return terraformer.BarrierRadius > 0f
            ? terraformer.BarrierRadius
            : terraformer.Radius;
    }

    private bool DeleteBarriers(TerraformerComponent terraformer)
    {
        if (terraformer.SpawnedBarriers.Count == 0)
            return false;

        var deletedAny = false;

        foreach (var barrier in terraformer.SpawnedBarriers)
        {
            if (!Deleted(barrier))
            {
                QueueDel(barrier);
                deletedAny = true;
            }
        }

        terraformer.SpawnedBarriers.Clear();
        return deletedAny;
    }

    private bool TryGetUsableTile(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tileIndices,
        out TileRef tile)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tileIndices, out tile))
            return false;

        if (tile.Tile.IsEmpty)
            return false;

        var tileDefinition = _tileDefinition[tile.Tile.TypeId];

        if (tileDefinition.ID == "Space")
            return false;

        return true;
    }

    private bool IsTileInsideRadius(Vector2i tile, Vector2i center, float radius)
    {
        var dx = tile.X - center.X;
        var dy = tile.Y - center.Y;

        return dx * dx + dy * dy <= radius * radius;
    }

    private bool IsTileBlockedByWall(MapGridComponent grid, Vector2i tileIndices)
    {
        foreach (var anchored in grid.GetAnchoredEntities(tileIndices))
        {
            if (Deleted(anchored))
                continue;

            if (HasComp<TerraformerBarrierComponent>(anchored))
                continue;

            var proto = MetaData(anchored).EntityPrototype;

            if (proto == null)
                continue;

            if (proto.ID == TerraformerBarrierPrototype)
                continue;

            if (proto.Components.ContainsKey("Airtight"))
                return true;
        }

        return false;
    }

    private bool TryTerraformOneTile(TerraformerComponent terraformer, TransformComponent xform)
    {
        if (xform.GridUid == null)
            return false;

        var gridUid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var validTiles = new List<TileRef>();
        var targetTile = _tileDefinition[terraformer.TargetTile];

        foreach (var tile in GetTilesInRadius(gridUid, grid, xform.Coordinates, terraformer.Radius))
        {
            var tileDefinition = _tileDefinition[tile.Tile.TypeId];

            if (!terraformer.SourceTiles.Contains(tileDefinition.ID))
                continue;

            if (tile.Tile.TypeId == targetTile.TileId)
                continue;

            validTiles.Add(tile);
        }

        if (validTiles.Count == 0)
            return false;

        var selectedTile = _random.Pick(validTiles);

        _map.SetTile(gridUid, grid, selectedTile.GridIndices, new Tile(targetTile.TileId));

        return true;
    }

    private void TryGenerateAtmosphere(TerraformerComponent terraformer, TransformComponent xform)
    {
        if (xform.GridUid == null)
            return;

        var gridUid = xform.GridUid.Value;
        var mapUid = xform.MapUid;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        foreach (var tile in GetTilesInRadius(gridUid, grid, xform.Coordinates, terraformer.Radius))
        {
            var mixture = _atmosphere.GetTileMixture(gridUid, mapUid, tile.GridIndices, true);

            if (mixture == null || mixture.Immutable)
                continue;

            if (mixture.Pressure >= terraformer.TargetPressure)
                continue;

            if (mixture.Pressure >= terraformer.MaxPressure)
                continue;

            var missingPressure = MathF.Max(terraformer.TargetPressure - mixture.Pressure, 0f);

            var temperature = MathF.Max(mixture.Temperature, Atmospherics.T20C);
            var maxMolesForPressureGap = missingPressure * mixture.Volume / (Atmospherics.R * temperature);

            var molesToAdd = MathF.Min(terraformer.GasMolesPerTile, maxMolesForPressureGap);

            if (molesToAdd <= Atmospherics.GasMinMoles)
                continue;

            mixture.AdjustMoles(Gas.Oxygen, molesToAdd * Atmospherics.OxygenStandard);
            mixture.AdjustMoles(Gas.Nitrogen, molesToAdd * Atmospherics.NitrogenStandard);
            mixture.Temperature = Atmospherics.T20C;
        }
    }

    private void TryScrubGases(TerraformerComponent terraformer, TransformComponent xform)
    {
        if (!terraformer.ScrubGases)
            return;

        if (terraformer.ScrubbedGases.Count == 0)
            return;

        if (xform.GridUid == null)
            return;

        var gridUid = xform.GridUid.Value;
        var mapUid = xform.MapUid;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        foreach (var tile in GetTilesInRadius(gridUid, grid, xform.Coordinates, terraformer.Radius))
        {
            var mixture = _atmosphere.GetTileMixture(gridUid, mapUid, tile.GridIndices, true);

            if (mixture == null || mixture.Immutable)
                continue;

            foreach (var gas in terraformer.ScrubbedGases)
            {
                var gasMoles = mixture.GetMoles(gas);

                if (gasMoles <= terraformer.TargetScrubMoles)
                    continue;

                var removable = gasMoles - terraformer.TargetScrubMoles;
                var molesToRemove = MathF.Min(terraformer.ScrubMolesPerTile, removable);

                if (molesToRemove <= Atmospherics.GasMinMoles)
                    continue;

                mixture.AdjustMoles(gas, -molesToRemove);
            }
        }
    }

    private void TrySpawnTree(EntityUid uid, TerraformerComponent terraformer, TransformComponent xform)
    {
        if (!terraformer.SpawnTrees)
            return;

        if (terraformer.SpawnedTrees >= terraformer.MaxSpawnedTrees)
            return;

        if (!_random.Prob(terraformer.TreeSpawnChance))
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

        Spawn(terraformer.TreePrototype, coords);
        terraformer.SpawnedTrees++;

        Dirty(uid, terraformer);
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
