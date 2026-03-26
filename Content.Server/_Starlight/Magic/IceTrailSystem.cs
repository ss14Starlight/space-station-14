// Starlight: Ice trail system for projectiles that leave ice/snow while traveling
using Content.Server.Atmos.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Magic;

/// <summary>
/// Starlight: System that spawns IceCrust debris while projectiles with IceTrailComponent are flying.
/// Creates a trail of frozen debris behind the projectile at regular intervals.
/// </summary>
public sealed class IceTrailSystem : EntitySystem
{
    // Starlight: System dependencies
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        // Starlight: Subscribe to update events to spawn ice periodically
        SubscribeLocalEvent<IceTrailComponent, ComponentStartup>(OnStartup);
    }

    /// <summary>
    /// Starlight: Initialize the component when added to an entity.
    /// </summary>
    private void OnStartup(Entity<IceTrailComponent> ent, ref ComponentStartup args) =>
        ent.Comp.TimeAccumulator = 0f;

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Starlight: Process all entities with IceTrailComponent
        var query = EntityQueryEnumerator<IceTrailComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            // Starlight: Accumulate time
            comp.TimeAccumulator += frameTime;

            // Starlight: Check if it's time to spawn ice/snow
            if (comp.TimeAccumulator < comp.SpawnInterval)
                continue;

            // Starlight: Reset accumulator
            comp.TimeAccumulator = 0f;

            // Starlight: Get the current position
            var coords = _transformSystem.GetMapCoordinates(uid, xform);

            // Starlight: Try to find the grid at this position
            if (!_mapManager.TryFindGridAt(coords, out var gridUid, out var grid))
                continue; // Starlight: No grid found, can't spawn ice

            // Starlight: Convert map coordinates to tile indices
            var centerIndices = _mapSystem.CoordinatesToTile(gridUid, grid, coords);
            
            // Starlight: Get all tiles in a 1.5-tile radius circle around the projectile
            var circle = new Circle(coords.Position, 1.5f); // Starlight: 1.5-tile radius for ice coverage
            var tilesToFreeze = _mapSystem.GetTilesIntersecting(gridUid, grid, circle, ignoreEmpty: false);

            // Starlight: Spawn ice debris or replace with snow tile on each tile in the circle
            foreach (var tileRef in tilesToFreeze)
            {
                // Starlight: Skip tiles that are empty/space
                if (tileRef.Tile.IsEmpty)
                    continue;

                // Starlight: Get the current tile's definition to check if it's already snow
                var currentTileDef = _tileDefManager[tileRef.Tile.TypeId];
                var isSnowTile = currentTileDef.ID == comp.SnowTileId;

                // Starlight: Determine whether to spawn ice or snow (55% ice, 45% snow)
                if (_random.Prob(comp.IceChance))
                {
                    // Starlight: Only spawn IceCrust if the tile is NOT already a snow tile
                    if (!isSnowTile)
                    {
                        var tileCenter = _mapSystem.GridTileToLocal(gridUid, grid, tileRef.GridIndices);
                        Spawn(comp.IceEntityId, tileCenter);
                    }
                }
                else
                {
                    // Starlight: Replace floor tile with snow (only if not already snow)
                    if (!isSnowTile && _tileDefManager.TryGetDefinition(comp.SnowTileId, out var tileDef))
                    {
                        var newTile = new Tile(tileDef.TileId);
                        _mapSystem.SetTile(gridUid, grid, tileRef.GridIndices, newTile);
                    }
                }

                // Starlight: Also freeze the atmosphere on this tile (make it very cold)
                if (_atmosphereSystem.GetTileMixture(gridUid, null, tileRef.GridIndices, true) is { } mixture)
                {
                    // Starlight: Set the temperature to freezing (50 Kelvin is very cold!)
                    mixture.Temperature = 50f;
                }
            }
        }
    }
}
