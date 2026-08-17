using System.Linq;
using Content.Server.Decals;
using Content.Server.Gravity;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Friction;
using Content.Shared.Gravity;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Noise;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Salvage.Ruins;

public sealed partial class RuinGeneratorSystem
{
    #region Dependencies

    private static readonly ProtoId<DamageTypePrototype> StructuralDamageType = "Structural";
    // Wreck-only biome: scrap/treasure/decals without thrusters, gyros, or mob spawners.
    private static readonly ProtoId<BiomeTemplatePrototype> SpaceRuinWreckBiome = "SpaceRuinWreck";
    private const float WreckFrictionModifier = 0.25f; // Makes it so the wreck doesn't slow down too much before it hits the station.

    [Dependency] private AnchorableSystem _anchorable = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private TileFrictionController _friction = default!;
    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedBiomeSystem _biome = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    #endregion

    #region Methods

    /// <summary>
    /// Spawns a ruin chunk grid on the given map from a <see cref="RuinResult"/>.
    /// Places floors, walls, windows, then wreck-safe biome loot/decals.
    /// </summary>
    public EntityUid? SpawnRuinGrid(MapId mapId, RuinResult result, int seed)
    {
        var ruinGrid = _mapManager.CreateGridEntity(mapId);
        _mapSystem.SetTiles(ruinGrid.Owner, ruinGrid.Comp, result.FloorTiles);

        foreach (var (wallPos, wallProto) in result.WallEntities)
        {
            var wallEntity = SpawnAtPosition(wallProto, new EntityCoordinates(ruinGrid.Owner, wallPos));
            var wallXform = Transform(wallEntity);
            if (!wallXform.Anchored)
                _transform.AnchorEntity((wallEntity, wallXform), (ruinGrid.Owner, ruinGrid.Comp), wallPos);
        }

        SpawnRuinWindows(ruinGrid.Owner, ruinGrid.Comp, result, seed);
        SpawnRuinBiomeEntities(ruinGrid.Owner, ruinGrid.Comp, result, seed);

        EnsureComp<GravityComponent>(ruinGrid.Owner);
        _gravity.EnableGravity(ruinGrid.Owner);
        _metaData.SetEntityName(ruinGrid.Owner, Loc.GetString("station-event-wreck-ruin-name"));

        if (!TryPrepareWreckGrid(ruinGrid.Owner))
            return null;

        return ruinGrid.Owner;
    }

    /// <summary>
    /// Strips shuttle impact handling and restores a free-flying dynamic body with wreck friction.
    /// Used by both generated ruins and fixed-grid wrecks.
    /// </summary>
    /// <returns>False if the grid had no physics and was queued for deletion.</returns>
    public bool TryPrepareWreckGrid(EntityUid grid)
    {
        // Keep the original wreck damping without making both colliding grids process shuttle impact damage.
        RemComp<ShuttleComponent>(grid);

        if (!TryComp<PhysicsComponent>(grid, out var physics))
        {
            QueueDel(grid);
            return false;
        }

        _physics.SetBodyType(grid, BodyType.Dynamic, body: physics);
        _physics.SetBodyStatus(grid, physics, BodyStatus.InAir);
        _physics.SetFixedRotation(grid, false, body: physics);

        var friction = EnsureComp<TileFrictionModifierComponent>(grid);
        _friction.SetModifier(grid, WreckFrictionModifier, friction);
        return true;
    }

    private void SpawnRuinWindows(EntityUid gridUid, MapGridComponent grid, RuinResult ruinResult, int seed)
    {
        var windowDamageChance = ruinResult.Config?.WindowDamageChance ?? 0f;
        var windowRand = new System.Random(seed);

        foreach (var (windowPos, windowProto, windowRotation) in ruinResult.WindowEntities)
        {
            var tileRef = _mapSystem.GetTileRef(gridUid, grid, windowPos);
            if (tileRef.Tile.IsEmpty)
                continue;

            var windowEntity = SpawnAttachedTo(windowProto, new EntityCoordinates(gridUid, windowPos), rotation: windowRotation);
            var windowXform = Transform(windowEntity);
            if (!windowXform.Anchored)
                _transform.AnchorEntity((windowEntity, windowXform), (gridUid, grid), windowPos);

            if (windowDamageChance <= 0f || windowRand.NextSingle() >= windowDamageChance)
                continue;

            if (!HasComp<DamageableComponent>(windowEntity))
                continue;

            var damage = new DamageSpecifier(
                _prototypeManager.Index(StructuralDamageType),
                FixedPoint2.New(25));
            _damageable.TryChangeDamage(windowEntity, damage);
        }
    }

    private void SpawnRuinBiomeEntities(EntityUid gridUid, MapGridComponent grid, RuinResult ruinResult, int seed)
    {
        var blockedPositions = new HashSet<Vector2i>(
            ruinResult.WallEntities.Select(w => w.Position)
                .Concat(ruinResult.WindowEntities.Select(w => w.Position)));

        if (!_prototypeManager.TryIndex(SpaceRuinWreckBiome, out var ruinTemplate))
            return;

        var layers = ruinTemplate.Layers;
        var noiseCache = new Dictionary<int, FastNoiseLite>();

        foreach (var (pos, _) in ruinResult.FloorTiles)
        {
            if (blockedPositions.Contains(pos))
                continue;

            var tileRef = _mapSystem.GetTileRef(gridUid, grid, pos);
            if (tileRef.Tile.IsEmpty)
                continue;

            if (_biome.TryGetDecals(pos, layers, seed, (gridUid, grid), out var decals, noiseCache))
            {
                foreach (var decal in decals)
                {
                    _decals.TryAddDecal(decal.ID, new EntityCoordinates(gridUid, decal.Position), out _);
                }
            }

            if (!_biome.TryGetEntity(pos, layers, tileRef.Tile, seed, (gridUid, grid), out var entityProto, noiseCache))
                continue;

            if (!_anchorable.TileFree((gridUid, grid), pos, (int)CollisionGroup.MachineLayer, (int)CollisionGroup.MachineLayer))
                continue;

            var entity = SpawnAtPosition(entityProto, new EntityCoordinates(gridUid, pos + grid.TileSizeHalfVector));
            var xform = Transform(entity);
            if (!xform.Anchored)
                _transform.AnchorEntity((entity, xform), (gridUid, grid), pos);
        }
    }

    #endregion
}
