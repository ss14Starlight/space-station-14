using Content.Server.Atmos.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Magic;

/// <summary>
/// System that handles spawning IceCrust debris in a circle pattern when a projectile with
/// IceSpawnOnTriggerComponent hits a humanoid entity.
///
/// This creates a visual area of frozen ground by spawning IceCrust entities
/// and freezing the atmosphere to create a realistic ice storm effect.
/// </summary>
public sealed class IceSpawnOnTriggerSystem : EntitySystem
{
    // System dependencies
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to trigger events from projectiles
        SubscribeLocalEvent<IceSpawnOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    /// <summary>
    /// Handles the trigger event when the ice projectile hits something.
    /// Spawns IceCrust debris in a circle if the target is a humanoid.
    /// </summary>
    private void OnTrigger(Entity<IceSpawnOnTriggerComponent> ent, ref TriggerEvent args)
    {
        // Get the user/target from the trigger event
        var target = args.User;

        // If no target, don't spawn ice
        if (target == null)
            return;

        // If we require a humanoid target, check for HumanoidAppearanceComponent and MobStateComponent
        if (ent.Comp.RequireHumanoid)
        {
            if (!HasComp<HumanoidAppearanceComponent>(target.Value) ||
                !HasComp<MobStateComponent>(target.Value))
            {
                return; // Not a living humanoid, don't spawn ice circle
            }
        }

        // Get the transform and coordinates of the target
        var targetXform = Transform(target.Value);
        var targetCoords = _transformSystem.GetMapCoordinates(targetXform);

        // Try to get the grid at the target's position
        if (!_mapManager.TryFindGridAt(targetCoords, out var gridUid, out var grid))
            return; // No grid found, can't spawn ice entities

        // Calculate which tiles are within the radius of the impact point
        var circle = new Circle(targetCoords.Position, ent.Comp.Radius);
        var tilesToFreeze = _mapSystem.GetTilesIntersecting(gridUid, grid, circle, ignoreEmpty: false);

        // Spawn ice debris or replace with snow tile on each tile in the circle
        foreach (var tileRef in tilesToFreeze)
        {
            // Skip tiles that are empty/space
            if (tileRef.Tile.IsEmpty)
                continue;

            // Get the current tile's definition to check if it's already snow
            var currentTileDef = _tileDefManager[tileRef.Tile.TypeId];
            var isSnowTile = currentTileDef.ID == ent.Comp.SnowTileId;

            // Determine whether to spawn ice or snow (60% ice, 40% snow)
            if (_random.Prob(ent.Comp.IceChance))
            {
                // Only spawn IceCrust if the tile is NOT already a snow tile
                if (!isSnowTile)
                {
                    var tileCenter = _mapSystem.GridTileToLocal(gridUid, grid, tileRef.GridIndices);
                    Spawn(ent.Comp.IceEntityId, tileCenter);
                }
            }
            else
            {
                // Replace floor tile with snow (only if not already snow)
                if (!isSnowTile && _tileDefManager.TryGetDefinition(ent.Comp.SnowTileId, out var tileDef))
                {
                    var newTile = new Tile(tileDef.TileId);
                    _mapSystem.SetTile(gridUid, grid, tileRef.GridIndices, newTile);
                }
            }

            // Also freeze the atmosphere on this tile (make it very cold)
            if (_atmosphereSystem.GetTileMixture(gridUid, null, tileRef.GridIndices, true) is { } mixture)
            {
                // Set the temperature to freezing (50 Kelvin is very cold!)
                mixture.Temperature = 50f;
            }
        }
    }
}
