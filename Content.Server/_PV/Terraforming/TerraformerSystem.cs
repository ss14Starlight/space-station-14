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

        // When a Terraformer is deleted, its Transform/Grid may already be unreliable.
        // So force all other active Terraformers to refresh instead of depending on same-grid lookup.
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

        var query = EntityQueryEnumerator<TerraformerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var terraformer, out var xform))
        {
            var poweredAndActive = terraformer.Active && IsPowered(uid);

            // If the machine is inactive or unpowered, remove the barrier ring.
            // If it had barriers, force every other active Terraformer to refresh.
            if (!poweredAndActive)
            {
                var deletedAny = DeleteBarriers(terraformer);

                if (deletedAny)
                    ForceAllOtherTerraformersToRefresh(uid);

                continue;
            }

            // Powered + active means the barrier should exist,
            // even if the Terraformer has no Biomass fuel.
            RefreshBarriersIfNeeded(uid, terraformer, xform, frameTime);

            // No fuel means:
            // keep barriers, but do not terraform, do not generate gas,
            // do not scrub gases, and do not consume fuel.
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

                // Science only happens if a tile was actually converted.
                if (TryTerraformOneTile(terraformer, xform))
                    AwardSciencePoints(uid, terraformer);
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

            Dirty(uid, terraformer);
        }
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

        // Award points to the first powered research client with an attached research server.
        // This avoids using TerraformingConsoleComponent completely.
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

    private void RefreshBarriersIfNeeded(
        EntityUid uid,
        TerraformerComponent terraformer,
        TransformComponent xform,
        float frameTime)
    {
        if (!terraformer.CreateBarriers)
            return;

        terraformer.BarrierRefreshAccumulator += frameTime;

        var shouldRefresh =
            terraformer.ForceBarrierRefresh ||
            terraformer.SpawnedBarriers.Count == 0 ||
            terraformer.BarrierRefreshAccumulator >= terraformer.BarrierRefreshCooldown;

        if (!shouldRefresh)
            return;

        terraformer.ForceBarrierRefresh = false;
        terraformer.BarrierRefreshAccumulator = 0f;

        DeleteBarriers(terraformer);
        EnsureBarriers(uid, terraformer, xform);

        Dirty(uid, terraformer);
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

    private void EnsureBarriers(EntityUid uid, TerraformerComponent terraformer, TransformComponent xform)
    {
        if (!terraformer.CreateBarriers)
            return;

        if (xform.GridUid == null)
            return;

        var gridUid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var barrierRadius = GetBarrierRadius(terraformer);

        foreach (var tile in GetBarrierTiles(gridUid, grid, xform.Coordinates, barrierRadius))
        {
            if (IsBarrierTileInsideOtherActiveTerraformer(uid, gridUid, grid, tile))
                continue;

            var coords = _map.GridTileToLocal(gridUid, grid, tile.GridIndices);

            var barrier = Spawn(terraformer.BarrierPrototype, coords);
            terraformer.SpawnedBarriers.Add(barrier);
        }
    }

    private bool IsBarrierTileInsideOtherActiveTerraformer(
        EntityUid ownerUid,
        EntityUid gridUid,
        MapGridComponent grid,
        TileRef barrierTile)
    {
        var query = EntityQueryEnumerator<TerraformerComponent, TransformComponent>();

        while (query.MoveNext(out var otherUid, out var otherTerraformer, out var otherXform))
        {
            if (otherUid == ownerUid)
                continue;

            if (!otherTerraformer.Active)
                continue;

            if (!IsPowered(otherUid))
                continue;

            if (otherXform.GridUid == null || otherXform.GridUid.Value != gridUid)
                continue;

            var otherCenterTile = _map.GetTileRef(gridUid, grid, otherXform.Coordinates);
            var otherBarrierRadius = GetBarrierRadius(otherTerraformer);

            var dx = barrierTile.GridIndices.X - otherCenterTile.GridIndices.X;
            var dy = barrierTile.GridIndices.Y - otherCenterTile.GridIndices.Y;

            var distance = MathF.Sqrt(dx * dx + dy * dy);

            // If this barrier tile would be inside another active Terraformer's field,
            // skip it so overlapping fields merge instead of creating internal walls.
            if (distance < otherBarrierRadius - 0.75f)
                return true;
        }

        return false;
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

    private IEnumerable<TileRef> GetBarrierTiles(
        EntityUid gridUid,
        MapGridComponent grid,
        EntityCoordinates center,
        float radius)
    {
        var centerTile = _map.GetTileRef(gridUid, grid, center);

        var directions = new[]
        {
            new Vector2i(1, 0),
            new Vector2i(-1, 0),
            new Vector2i(0, 1),
            new Vector2i(0, -1)
        };

        var reachable = new HashSet<Vector2i>();
        var queue = new Queue<Vector2i>();

        var centerIndices = centerTile.GridIndices;

        if (!IsTileInsideRadius(centerIndices, centerIndices, radius))
            yield break;

        if (!TryGetUsableTile(gridUid, grid, centerIndices, out _))
            yield break;

        if (IsTileBlockedByWall(grid, centerIndices))
            yield break;

        reachable.Add(centerIndices);
        queue.Enqueue(centerIndices);

        // Flood-fill outward from the Terraformer.
        // This prevents the barrier from passing through walls/windows/airlocks
        // and prevents it from extending into empty/space/off-grid tiles.
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var direction in directions)
            {
                var neighbor = current + direction;

                if (reachable.Contains(neighbor))
                    continue;

                if (!IsTileInsideRadius(neighbor, centerIndices, radius))
                    continue;

                // Empty/space/off-grid tiles count as outside the usable area.
                if (!TryGetUsableTile(gridUid, grid, neighbor, out _))
                    continue;

                if (IsTileBlockedByWall(grid, neighbor))
                    continue;

                reachable.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        // Any reachable tile touching outside radius, off-grid, empty/space,
        // wall, or unreachable space becomes part of the barrier boundary.
        foreach (var indices in reachable)
        {
            var isBoundary = false;

            foreach (var direction in directions)
            {
                var neighbor = indices + direction;

                if (!IsTileInsideRadius(neighbor, centerIndices, radius))
                {
                    isBoundary = true;
                    break;
                }

                // Important grid-following logic:
                // if the neighbor is empty/space/off-grid, this tile is the edge.
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

                if (!reachable.Contains(neighbor))
                {
                    isBoundary = true;
                    break;
                }
            }

            if (!isBoundary)
                continue;

            if (!TryGetUsableTile(gridUid, grid, indices, out var tile))
                continue;

            yield return tile;
        }
    }

    private bool TryGetUsableTile(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tileIndices,
        out TileRef tile)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tileIndices, out tile))
            return false;

        // Empty tiles count as outside the usable grid/asteroid shape.
        // This prevents barriers from spawning out in empty space.
        if (tile.Tile.IsEmpty)
            return false;

        var tileDefinition = _tileDefinition[tile.Tile.TypeId];

        // Space should also count as outside.
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

            var proto = MetaData(anchored).EntityPrototype;

            if (proto == null)
                continue;

            // Do not treat Terraformer barriers themselves as walls.
            if (proto.ID == TerraformerBarrierPrototype)
                continue;

            // Avoid direct AirtightComponent reference because this fork does not expose that type here.
            // Most walls/windows/airlocks that block atmos have this component in their prototype.
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

            // Extra safety: never count a tile that is already the target tile.
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