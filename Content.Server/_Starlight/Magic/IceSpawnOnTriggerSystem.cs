// Starlight: Ice Storm spell system
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Magic;

/// <summary>
/// Starlight: System that handles spawning IceCrust debris in a circle pattern when a projectile with
/// IceSpawnOnTriggerComponent hits a humanoid entity.
/// 
/// This creates a visual area of frozen ground by spawning IceCrust entities
/// and freezing the atmosphere to create a realistic ice storm effect.
/// </summary>
public sealed class IceSpawnOnTriggerSystem : EntitySystem
{
    // Starlight: System dependencies
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

        // Starlight: Subscribe to trigger events from projectiles
        SubscribeLocalEvent<IceSpawnOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    /// <summary>
    /// Starlight: Handles the trigger event when the ice projectile hits something.
    /// Spawns IceCrust debris in a circle if the target is a humanoid.
    /// </summary>
    private void OnTrigger(Entity<IceSpawnOnTriggerComponent> ent, ref TriggerEvent args)
    {
        // Starlight: Get the user/target from the trigger event
        var target = args.User;
        
        // Starlight: If no target, don't spawn ice
        if (target == null)
            return;

        // Starlight: If we require a humanoid target, check for HumanoidAppearanceComponent and MobStateComponent
        if (ent.Comp.RequireHumanoid)
        {
            if (!HasComp<HumanoidAppearanceComponent>(target.Value) || 
                !HasComp<MobStateComponent>(target.Value))
            {
                return; // Starlight: Not a living humanoid, don't spawn ice circle
            }
        }

        // Starlight: Get the transform and coordinates of the target
        var targetXform = Transform(target.Value);
        var targetCoords = _transformSystem.GetMapCoordinates(targetXform);

        // Starlight: Try to get the grid at the target's position
        if (!_mapManager.TryFindGridAt(targetCoords, out var gridUid, out var grid))
            return; // Starlight: No grid found, can't spawn ice entities

        // Starlight: Calculate which tiles are within the radius of the impact point
        var circle = new Circle(targetCoords.Position, ent.Comp.Radius);
        var tilesToFreeze = _mapSystem.GetTilesIntersecting(gridUid, grid, circle, ignoreEmpty: false);

        // Starlight: Spawn ice debris or replace with snow tile on each tile in the circle
        foreach (var tileRef in tilesToFreeze)
        {
            // Starlight: Skip tiles that are empty/space
            if (tileRef.Tile.IsEmpty)
                continue;

            // Starlight: Get the current tile's definition to check if it's already snow
            var currentTileDef = _tileDefManager[tileRef.Tile.TypeId];
            var isSnowTile = currentTileDef.ID == ent.Comp.SnowTileId;

            // Starlight: Determine whether to spawn ice or snow (60% ice, 40% snow)
            if (_random.Prob(ent.Comp.IceChance))
            {
                // Starlight: Only spawn IceCrust if the tile is NOT already a snow tile
                if (!isSnowTile)
                {
                    var tileCenter = _mapSystem.GridTileToLocal(gridUid, grid, tileRef.GridIndices);
                    Spawn(ent.Comp.IceEntityId, tileCenter);
                }
            }
            else
            {
                // Starlight: Replace floor tile with snow (only if not already snow)
                if (!isSnowTile && _tileDefManager.TryGetDefinition(ent.Comp.SnowTileId, out var tileDef))
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
