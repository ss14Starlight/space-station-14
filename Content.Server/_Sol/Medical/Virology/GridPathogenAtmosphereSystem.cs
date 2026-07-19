using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Atmos;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Tile-based airborne pathogen store with airtight-aware diffusion and breathing exposure.
/// Invisible to gas analyzers; contributes zero pressure.
/// </summary>
public sealed class GridPathogenAtmosphereSystem : EntitySystem
{
    private static readonly AtmosDirection[] CardinalDirs =
    [
        AtmosDirection.North,
        AtmosDirection.South,
        AtmosDirection.East,
        AtmosDirection.West,
    ];

    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly PathogenSystem _pathogen = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<GridPathogenAtmosphereComponent, MapGridComponent>();
        while (query.MoveNext(out var gridUid, out var store, out var grid))
        {
            if (store.Tiles.Count == 0)
                continue;

            TickGrid((gridUid, store, grid));
        }

        ExposeMobsFromTiles();
    }

    private void TickGrid(Entity<GridPathogenAtmosphereComponent, MapGridComponent> grid)
    {
        var (gridUid, store, mapGrid) = grid;
        var next = new Dictionary<Vector2i, Dictionary<string, float>>();

        foreach (var (tile, pathogens) in store.Tiles)
        {
            foreach (var (pathogenId, load) in pathogens)
            {
                if (!_pathogen.TryResolvePathogen(pathogenId, out var pathogen) || pathogen == null)
                    continue;

                var decayed = load - pathogen.EnvironmentalDecayPerSecond;
                if (decayed <= 0.05f)
                    continue;

                // Keep most load on the source tile.
                var remain = decayed * 0.8f;
                AddToBuffer(next, tile, pathogenId, remain);

                var transferPool = decayed - remain;
                var openDirs = 0;
                foreach (var dir in CardinalDirs)
                {
                    if (_atmos.IsTileAirBlocked(gridUid, tile, dir, mapGrid))
                        continue;
                    openDirs++;
                }

                if (openDirs == 0 || transferPool <= 0.01f)
                {
                    AddToBuffer(next, tile, pathogenId, transferPool);
                    continue;
                }

                var perDir = transferPool / openDirs;
                foreach (var dir in CardinalDirs)
                {
                    if (_atmos.IsTileAirBlocked(gridUid, tile, dir, mapGrid))
                        continue;

                    var neighbor = tile.Offset(dir);
                    // Neighbor must also not block the opposite direction.
                    if (_atmos.IsTileAirBlocked(gridUid, neighbor, dir.GetOpposite(), mapGrid))
                        continue;

                    AddToBuffer(next, neighbor, pathogenId, perDir);
                }
            }
        }

        store.Tiles = next;
        Dirty(gridUid, store);
    }

    private static void AddToBuffer(
        Dictionary<Vector2i, Dictionary<string, float>> buffer,
        Vector2i tile,
        string pathogenId,
        float load)
    {
        if (load <= 0.01f)
            return;

        if (!buffer.TryGetValue(tile, out var pathogens))
        {
            pathogens = new Dictionary<string, float>();
            buffer[tile] = pathogens;
        }

        pathogens.TryGetValue(pathogenId, out var existing);
        pathogens[pathogenId] = existing + load;
    }

    private void ExposeMobsFromTiles()
    {
        var mobQuery = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var mob, out _, out var xform))
        {
            if (!_pathogen.IsVirologyEnabledAt(mob))
                continue;

            if (!_pathogen.TryGetVirologyStation(mob, out _, out var station) || !station.AllowAirborne)
                continue;

            if (xform.GridUid is not { } gridUid)
                continue;

            if (!TryComp<GridPathogenAtmosphereComponent>(gridUid, out var store))
                continue;

            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var tile = _map.GetTileRef(gridUid, grid, xform.Coordinates).GridIndices;
            if (!store.Tiles.TryGetValue(tile, out var pathogens))
                continue;

            foreach (var (pathogenId, load) in pathogens)
            {
                if (load < 0.2f)
                    continue;

                _pathogen.TryExpose(mob, pathogenId, load * 0.1f, PathogenTransmission.Airborne);
            }
        }
    }

    public void AddAirborneLoad(EntityUid source, string pathogenId, float load)
    {
        if (load <= 0f)
            return;

        var xform = Transform(source);
        if (xform.GridUid is not { } gridUid)
            return;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        EnsureComp<GridPathogenAtmosphereComponent>(gridUid);
        var store = Comp<GridPathogenAtmosphereComponent>(gridUid);
        var tile = _map.GetTileRef(gridUid, grid, xform.Coordinates).GridIndices;

        if (!store.Tiles.TryGetValue(tile, out var pathogens))
        {
            pathogens = new Dictionary<string, float>();
            store.Tiles[tile] = pathogens;
        }

        pathogens.TryGetValue(pathogenId, out var existing);
        pathogens[pathogenId] = existing + load;
        Dirty(gridUid, store);
    }

    public float GetAirborneLoad(EntityUid gridUid, Vector2i tile, string? pathogenId = null)
    {
        if (!TryComp<GridPathogenAtmosphereComponent>(gridUid, out var store))
            return 0f;

        if (!store.Tiles.TryGetValue(tile, out var pathogens))
            return 0f;

        if (pathogenId != null)
            return pathogens.GetValueOrDefault(pathogenId);

        var total = 0f;
        foreach (var load in pathogens.Values)
            total += load;
        return total;
    }

    public float RemoveAirborneLoad(EntityUid gridUid, Vector2i tile, float amount, string? pathogenId = null)
    {
        if (!TryComp<GridPathogenAtmosphereComponent>(gridUid, out var store))
            return 0f;

        if (!store.Tiles.TryGetValue(tile, out var pathogens) || pathogens.Count == 0)
            return 0f;

        var removed = 0f;
        var keys = pathogens.Keys.ToList();
        foreach (var id in keys)
        {
            if (pathogenId != null && id != pathogenId)
                continue;

            var take = Math.Min(pathogens[id], amount - removed);
            pathogens[id] -= take;
            removed += take;
            if (pathogens[id] <= 0.01f)
                pathogens.Remove(id);
            if (removed >= amount)
                break;
        }

        if (pathogens.Count == 0)
            store.Tiles.Remove(tile);

        Dirty(gridUid, store);
        return removed;
    }

    public float ScrubAround(EntityUid scrubber, float amount, float rangeTiles)
    {
        var xform = Transform(scrubber);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return 0f;

        if (!TryComp<GridPathogenAtmosphereComponent>(gridUid, out _))
            return 0f;

        var center = _map.GetTileRef(gridUid, grid, xform.Coordinates).GridIndices;
        var removed = 0f;
        var radius = (int)MathF.Ceiling(rangeTiles);

        for (var x = -radius; x <= radius; x++)
        for (var y = -radius; y <= radius; y++)
        {
            if (x * x + y * y > rangeTiles * rangeTiles)
                continue;

            var tile = new Vector2i(center.X + x, center.Y + y);
            removed += RemoveAirborneLoad(gridUid, tile, amount - removed);
            if (removed >= amount)
                return removed;
        }

        return removed;
    }
}
