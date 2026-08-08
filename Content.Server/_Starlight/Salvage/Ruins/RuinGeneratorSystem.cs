using System.Linq;
using Content.Shared._Starlight.Salvage.Ruins;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Salvage.Ruins;

/// <summary>
/// Generates irregular station ruin chunks from station map YAML via cost-based flood-fill.
/// Cost maps are built lazily on first use and cached for the rest of the round.
/// </summary>
public sealed partial class RuinGeneratorSystem : EntitySystem
{
    #region Dependencies

    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitionManager = default!;

    #endregion

    #region Fields

    private ISawmill _sawmill = default!;

    /// <summary>
    /// Cached map data keyed by map path. Built on demand to avoid round-start hitch from parsing full stations.
    /// </summary>
    private readonly Dictionary<ResPath, CachedMapData> _cachedMapData = new();

    private sealed class CachedMapData
    {
        public Dictionary<Vector2i, int> CostMap = new();
        public Dictionary<Vector2i, string> CoordinateMap = new();
        public List<(Vector2i Position, string PrototypeId)> WallEntities = new();
        public List<(Vector2i Position, string PrototypeId, Angle Rotation)> WindowEntities = new();
        /// <summary>Positions with finite cost, for O(1) random start picks without Keys.ToList each generate.</summary>
        public List<Vector2i> ValidStartPositions = new();
        public HashSet<Vector2i> WallPositions = new();
        public Dictionary<Vector2i, string> WallsByPosition = new();
        public Dictionary<Vector2i, (string PrototypeId, Angle Rotation)> WindowsByPosition = new();
    }

    /// <summary>
    /// Result of ruin generation: floors, walls, and windows ready to spawn.
    /// </summary>
    public sealed class RuinResult
    {
        public List<(Vector2i Position, Tile Tile)> FloorTiles = new();
        public List<(Vector2i Position, string PrototypeId)> WallEntities = new();
        public List<(Vector2i Position, string PrototypeId, Angle Rotation)> WindowEntities = new();
        public Box2 Bounds;
        public RuinChunkConfigPrototype? Config;
    }

    #endregion

    #region Methods

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("ruin.generator");
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        // Only clear when ruin maps change; do not rebuild all maps here (avoids reload hitch).
        if (!args.WasModified<RuinMapPrototype>())
            return;

        _sawmill.Info("RuinMapPrototype reloaded, clearing cost map cache");
        _cachedMapData.Clear();
    }

    /// <summary>
    /// Generates a ruin chunk from the specified map using multi-stage flood-fill.
    /// </summary>
    public RuinResult? GenerateRuin(ResPath mapPath, int seed, RuinChunkConfigPrototype? config = null)
    {
        if (!_cachedMapData.TryGetValue(mapPath, out var cachedData))
        {
            _sawmill.Info($"Building cost map for {mapPath} on-demand...");
            if (!BuildCostMapForMap(mapPath) || !_cachedMapData.TryGetValue(mapPath, out cachedData))
            {
                _sawmill.Error($"Failed to build cost map for {mapPath}");
                return null;
            }
        }

        config ??= GetDefaultConfig();
        if (config == null)
        {
            _sawmill.Error("No RuinChunkConfigPrototype available (expected Default)");
            return null;
        }

        var rand = new System.Random(seed);
        var startPos = FindValidStartLocation(cachedData, rand, maxRetries: 10, config.SpaceCost);
        if (!startPos.HasValue)
        {
            _sawmill.Error($"Failed to find valid start location for map {mapPath} with seed {seed}");
            return null;
        }

        // Walls + windows block flood expansion so room shells stay coherent when pulled in after each stage.
        var allBlockingPositions = new HashSet<Vector2i>(cachedData.WallPositions);
        allBlockingPositions.UnionWith(cachedData.WindowsByPosition.Keys);

        var region = FloodFillMultiStage(
            cachedData.CostMap,
            startPos.Value,
            config.FloodFillStages,
            config.FloodFillPoints,
            allBlockingPositions,
            rand,
            config.SpaceCost,
            config.DefaultTileCost);

        if (region.Count == 0)
        {
            _sawmill.Error($"Flood-fill returned empty region for map {mapPath} with seed {seed}");
            return null;
        }

        return BuildRuinResult(region, cachedData, config, seed);
    }

    private RuinResult? BuildRuinResult(
        HashSet<Vector2i> region,
        CachedMapData cachedData,
        RuinChunkConfigPrototype config,
        int seed)
    {
        var result = new RuinResult();
        var tilesToPlace = new Dictionary<Vector2i, Tile>();
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        foreach (var pos in region)
        {
            minX = Math.Min(minX, pos.X);
            minY = Math.Min(minY, pos.Y);
            maxX = Math.Max(maxX, pos.X);
            maxY = Math.Max(maxY, pos.Y);
        }

        var adjacentWallEntities = CollectAdjacentWalls(cachedData, region, ref minX, ref minY, ref maxX, ref maxY);
        var windowEntitiesInRegion = CollectRegionWindows(cachedData, region, ref minX, ref minY, ref maxX, ref maxY);

        var originX = minX;
        var originY = minY;
        var damageRand = new System.Random(seed);

        foreach (var pos in region)
        {
            if (!cachedData.CoordinateMap.TryGetValue(pos, out var tileId))
                continue;

            if (!_prototypeManager.TryIndex<ContentTileDefinition>(tileId, out var tileDef))
                continue;

            var isLattice = tileDef.ID.Equals("Lattice", StringComparison.OrdinalIgnoreCase);
            Tile tile;

            if (!isLattice && config.FloorToLatticeChance > 0f && damageRand.NextSingle() < config.FloorToLatticeChance)
            {
                if (!_tileDefinitionManager.TryGetDefinition("Lattice", out var latticeDef))
                    tile = new Tile(tileDef.TileId);
                else
                    tile = new Tile(latticeDef.TileId);
            }
            else
            {
                tile = new Tile(tileDef.TileId);
            }

            tilesToPlace[new Vector2i(pos.X - originX, pos.Y - originY)] = tile;
        }

        // Ensure floors under adjacent walls so walls are not floating in space.
        foreach (var (wallPos, _) in adjacentWallEntities)
        {
            var normalizedWallPos = new Vector2i(wallPos.X - originX, wallPos.Y - originY);
            if (tilesToPlace.ContainsKey(normalizedWallPos))
                continue;

            Tile floorTile;
            if (cachedData.CoordinateMap.TryGetValue(wallPos, out var tileId) &&
                _prototypeManager.TryIndex<ContentTileDefinition>(tileId, out var tileDef))
            {
                floorTile = new Tile(tileDef.TileId);
            }
            else if (_tileDefinitionManager.TryGetDefinition("Plating", out var platingDef))
            {
                floorTile = new Tile(platingDef.TileId);
            }
            else
            {
                continue;
            }

            tilesToPlace[normalizedWallPos] = floorTile;
        }

        foreach (var (wallPos, wallProto) in adjacentWallEntities)
        {
            if (config.WallDestroyChance > 0f && damageRand.NextSingle() < config.WallDestroyChance)
                continue;

            result.WallEntities.Add((new Vector2i(wallPos.X - originX, wallPos.Y - originY), wallProto));
        }

        foreach (var (windowPos, windowProto, windowRotation) in windowEntitiesInRegion)
        {
            result.WindowEntities.Add((new Vector2i(windowPos.X - originX, windowPos.Y - originY), windowProto, windowRotation));
        }

        if (tilesToPlace.Count == 0)
        {
            _sawmill.Error("No tiles to place after processing flood-fill region");
            return null;
        }

        result.FloorTiles = tilesToPlace.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        result.Bounds = new Box2(0, 0, maxX - originX + 1, maxY - originY + 1);
        result.Config = config;
        return result;
    }

    /// <summary>
    /// Walks region tiles and pulls in wall neighbors via HashSet lookup (O(region) not O(all walls on map)).
    /// </summary>
    private static List<(Vector2i Position, string PrototypeId)> CollectAdjacentWalls(
        CachedMapData cachedData,
        HashSet<Vector2i> regionSet,
        ref int minX,
        ref int minY,
        ref int maxX,
        ref int maxY)
    {
        var adjacent = new List<(Vector2i Position, string PrototypeId)>();
        var added = new HashSet<Vector2i>();

        foreach (var pos in regionSet)
        {
            for (var i = 0; i < CardinalOffsets.Length; i++)
            {
                var neighbor = pos + CardinalOffsets[i];
                if (!cachedData.WallsByPosition.TryGetValue(neighbor, out var wallProto))
                    continue;

                if (!added.Add(neighbor))
                    continue;

                adjacent.Add((neighbor, wallProto));
                minX = Math.Min(minX, neighbor.X);
                minY = Math.Min(minY, neighbor.Y);
                maxX = Math.Max(maxX, neighbor.X);
                maxY = Math.Max(maxY, neighbor.Y);
            }
        }

        return adjacent;
    }

    private static List<(Vector2i Position, string PrototypeId, Angle Rotation)> CollectRegionWindows(
        CachedMapData cachedData,
        HashSet<Vector2i> regionSet,
        ref int minX,
        ref int minY,
        ref int maxX,
        ref int maxY)
    {
        var windows = new List<(Vector2i Position, string PrototypeId, Angle Rotation)>();
        var added = new HashSet<Vector2i>();

        foreach (var pos in regionSet)
        {
            TryIncludeWindow(pos, cachedData, added, windows, ref minX, ref minY, ref maxX, ref maxY);

            for (var i = 0; i < CardinalOffsets.Length; i++)
                TryIncludeWindow(pos + CardinalOffsets[i], cachedData, added, windows, ref minX, ref minY, ref maxX, ref maxY);
        }

        return windows;
    }

    private static void TryIncludeWindow(
        Vector2i candidate,
        CachedMapData cachedData,
        HashSet<Vector2i> added,
        List<(Vector2i Position, string PrototypeId, Angle Rotation)> windows,
        ref int minX,
        ref int minY,
        ref int maxX,
        ref int maxY)
    {
        if (!cachedData.WindowsByPosition.TryGetValue(candidate, out var window))
            return;

        if (!added.Add(candidate))
            return;

        windows.Add((candidate, window.PrototypeId, window.Rotation));
        minX = Math.Min(minX, candidate.X);
        minY = Math.Min(minY, candidate.Y);
        maxX = Math.Max(maxX, candidate.X);
        maxY = Math.Max(maxY, candidate.Y);
    }

    private RuinChunkConfigPrototype? GetDefaultConfig()
    {
        _prototypeManager.TryIndex(new ProtoId<RuinChunkConfigPrototype>("Default"), out var config);
        return config;
    }

    #endregion
}
