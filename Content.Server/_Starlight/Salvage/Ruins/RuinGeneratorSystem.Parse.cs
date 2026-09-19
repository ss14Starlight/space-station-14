using System.Globalization;
using System.IO;
using System.Linq;
using Content.Shared._Starlight.Salvage.Ruins;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Salvage.Ruins;

public sealed partial class RuinGeneratorSystem
{
    #region Dependencies

    [Dependency] private MapLoaderSystem _mapLoader = default!;

    #endregion

    #region Methods

    private bool BuildCostMapForMap(ResPath mapPath)
    {
        if (!_mapLoader.TryReadFile(mapPath, out var mapData))
        {
            _sawmill.Error($"Failed to read map file: {mapPath}");
            return false;
        }

        if (!TryParseTileMap(mapData, mapPath, out var tileMap))
            return false;

        if (!mapData.TryGet("entities", out SequenceDataNode? entitiesNode))
        {
            _sawmill.Error($"Map file {mapPath} missing entities section");
            return false;
        }

        // Multi-grid maps are unsupported; only the first grid UID is used as the ruin source.
        if (!TryGetFirstGridUid(mapData, entitiesNode, mapPath, out var firstGridUid))
            return false;

        if (!TryGetMapGridComponent(entitiesNode, firstGridUid, mapPath, out var mapGridComponent))
            return false;

        if (!mapGridComponent.TryGet("chunks", out MappingDataNode? chunksNode))
        {
            _sawmill.Error($"Map file {mapPath} MapGrid component missing chunks section");
            return false;
        }

        ushort chunkSize = 16;
        if (mapGridComponent.TryGet("chunksize", out ValueDataNode? chunkSizeNode) &&
            ushort.TryParse(chunkSizeNode.Value, out var parsedChunkSize))
        {
            chunkSize = parsedChunkSize;
        }

        var coordinateMap = ParseChunks(chunksNode, tileMap, chunkSize, mapPath);
        if (coordinateMap.Count == 0)
        {
            _sawmill.Error($"Map file {mapPath} produced empty coordinate map");
            return false;
        }

        var defaultConfig = GetDefaultConfig();
        if (defaultConfig == null)
        {
            _sawmill.Error($"Cannot build cost map for {mapPath}: no RuinChunkConfigPrototype available (expected Default)");
            return false;
        }

        var wallEntities = ParseWallEntities(entitiesNode, firstGridUid, defaultConfig);
        var windowEntities = ParseWindowEntities(entitiesNode, firstGridUid, defaultConfig);
        var costMap = BuildCostMap(coordinateMap, windowEntities, wallEntities, defaultConfig);

        var wallsByPosition = new Dictionary<Vector2i, string>();
        foreach (var (pos, proto) in wallEntities)
            wallsByPosition[pos] = proto;

        var windowsByPosition = new Dictionary<Vector2i, (string, Angle)>();
        foreach (var (pos, proto, rot) in windowEntities)
            windowsByPosition[pos] = (proto, rot);

        _cachedMapData[mapPath] = new CachedMapData
        {
            CostMap = costMap,
            CoordinateMap = coordinateMap,
            WallEntities = wallEntities,
            WindowEntities = windowEntities,
            ValidStartPositions = costMap.Keys.ToList(),
            WallPositions = wallsByPosition.Keys.ToHashSet(),
            WallsByPosition = wallsByPosition,
            WindowsByPosition = windowsByPosition,
        };

        _sawmill.Debug($"Cached cost map for {mapPath}: {costMap.Count} tiles, {wallEntities.Count} walls, {windowEntities.Count} windows");
        return true;
    }

    private bool TryParseTileMap(MappingDataNode mapData, ResPath mapPath, out Dictionary<int, string> tileMap)
    {
        tileMap = new Dictionary<int, string>();

        if (!mapData.TryGet("tilemap", out MappingDataNode? tilemapNode))
        {
            _sawmill.Error($"Map file {mapPath} missing tilemap section");
            return false;
        }

        foreach (var (key, valueNode) in tilemapNode.Children)
        {
            if (valueNode is not ValueDataNode valueValue)
                continue;

            if (!int.TryParse(key, out var yamlTileId))
                continue;

            tileMap[yamlTileId] = valueValue.Value;
        }

        if (tileMap.Count == 0)
        {
            _sawmill.Error($"Map file {mapPath} has empty tilemap");
            return false;
        }

        return true;
    }

    private bool TryGetFirstGridUid(
        MappingDataNode mapData,
        SequenceDataNode entitiesNode,
        ResPath mapPath,
        out int firstGridUid)
    {
        firstGridUid = 0;

        if (mapData.TryGet("grids", out SequenceDataNode? gridsNode) && gridsNode.Sequence.Count > 0)
        {
            if (gridsNode.Sequence[0] is ValueDataNode firstGridUidNode &&
                int.TryParse(firstGridUidNode.Value, out firstGridUid))
            {
                return true;
            }

            _sawmill.Error($"Map file {mapPath} first grid UID is invalid");
            return false;
        }

        // Older format-6 maps (including /Maps/Test/floor3x3.yml) omit the grids list.
        if (TryFindFirstMapGridUid(entitiesNode, out firstGridUid))
            return true;

        _sawmill.Error($"Map file {mapPath} missing or empty grids section");
        return false;
    }

    private static bool TryFindFirstMapGridUid(SequenceDataNode entitiesNode, out int gridUid)
    {
        gridUid = 0;

        foreach (var protoGroup in entitiesNode.Sequence)
        {
            if (protoGroup is not MappingDataNode protoGroupNode)
                continue;

            if (!protoGroupNode.TryGet("entities", out SequenceDataNode? entitiesInGroup) || entitiesInGroup == null)
                continue;

            foreach (var entity in entitiesInGroup.Sequence)
            {
                if (entity is not MappingDataNode entityNode)
                    continue;

                if (!entityNode.TryGet("uid", out ValueDataNode? uidNode) || uidNode == null)
                    continue;

                if (!int.TryParse(uidNode.Value, out var entityUid))
                    continue;

                if (!entityNode.TryGet("components", out SequenceDataNode? componentsNode) || componentsNode == null)
                    continue;

                foreach (var component in componentsNode.Sequence)
                {
                    if (component is not MappingDataNode componentNode)
                        continue;

                    if (!componentNode.TryGet("type", out ValueDataNode? typeNode) || typeNode == null)
                        continue;

                    if (typeNode.Value != "MapGrid")
                        continue;

                    gridUid = entityUid;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetMapGridComponent(
        SequenceDataNode entitiesNode,
        int firstGridUid,
        ResPath mapPath,
        out MappingDataNode mapGridComponent)
    {
        mapGridComponent = null!;
        MappingDataNode? gridEntityNode = null;

        foreach (var protoGroup in entitiesNode.Sequence)
        {
            if (protoGroup is not MappingDataNode protoGroupNode)
                continue;

            if (!protoGroupNode.TryGet("entities", out SequenceDataNode? entitiesInGroup) || entitiesInGroup == null)
                continue;

            foreach (var entity in entitiesInGroup.Sequence)
            {
                if (entity is not MappingDataNode entityNode)
                    continue;

                if (!entityNode.TryGet("uid", out ValueDataNode? uidNode) || uidNode == null)
                    continue;

                if (!int.TryParse(uidNode.Value, out var entityUid) || entityUid != firstGridUid)
                    continue;

                gridEntityNode = entityNode;
                break;
            }

            if (gridEntityNode != null)
                break;
        }

        if (gridEntityNode == null)
        {
            _sawmill.Error($"Map file {mapPath} grid entity with UID {firstGridUid} not found");
            return false;
        }

        if (!gridEntityNode.TryGet("components", out SequenceDataNode? componentsNode) || componentsNode == null)
        {
            _sawmill.Error($"Map file {mapPath} grid entity missing components section");
            return false;
        }

        foreach (var component in componentsNode.Sequence)
        {
            if (component is not MappingDataNode componentNode)
                continue;

            if (!componentNode.TryGet("type", out ValueDataNode? typeNode) || typeNode == null)
                continue;

            if (typeNode.Value != "MapGrid")
                continue;

            mapGridComponent = componentNode;
            return true;
        }

        _sawmill.Error($"Map file {mapPath} grid entity missing MapGrid component");
        return false;
    }

    private Dictionary<Vector2i, string> ParseChunks(
        MappingDataNode chunksNode,
        Dictionary<int, string> tileMap,
        ushort chunkSize,
        ResPath mapPath)
    {
        var coordinateMap = new Dictionary<Vector2i, string>();

        foreach (var (chunkIndexStr, chunkValueNode) in chunksNode.Children)
        {
            var chunkIndexParts = chunkIndexStr.Split(',');
            if (chunkIndexParts.Length != 2 ||
                !int.TryParse(chunkIndexParts[0], out var chunkX) ||
                !int.TryParse(chunkIndexParts[1], out var chunkY))
            {
                continue;
            }

            if (chunkValueNode is not MappingDataNode chunkNode)
                continue;

            if (!chunkNode.TryGet("tiles", out ValueDataNode? tilesNode) || tilesNode == null)
                continue;

            // Map chunk format: version defaults to 7 when omitted (Robust map serialization).
            var version = 7;
            if (chunkNode.TryGet("version", out ValueDataNode? versionNode))
            {
                if (!int.TryParse(versionNode.Value, out var parsedVersion))
                {
                    _sawmill.Warning($"Invalid chunk version '{versionNode.Value}' in {mapPath} chunk {chunkIndexStr}");
                    continue;
                }

                version = parsedVersion;
            }

            byte[] tileBytes;
            try
            {
                tileBytes = Convert.FromBase64String(tilesNode.Value);
            }
            catch (FormatException ex)
            {
                _sawmill.Warning($"Invalid Base64 tile data in {mapPath} chunk {chunkIndexStr}: {ex.Message}");
                continue;
            }

            // Bytes per tile: v7 = int32 + 3 bytes; older = u16/int32 + 2 bytes.
            var bytesPerTile = version >= 7 ? 7 : version < 6 ? 4 : 6;
            var expectedBytes = chunkSize * chunkSize * bytesPerTile;
            if (tileBytes.Length < expectedBytes)
            {
                _sawmill.Warning(
                    $"Short tile buffer in {mapPath} chunk {chunkIndexStr}: got {tileBytes.Length}, expected >= {expectedBytes} (version {version})");
                continue;
            }

            using var stream = new MemoryStream(tileBytes);
            using var reader = new BinaryReader(stream);

            for (ushort y = 0; y < chunkSize; y++)
            {
                for (ushort x = 0; x < chunkSize; x++)
                {
                    int yamlTileId;
                    if (version >= 7)
                    {
                        yamlTileId = reader.ReadInt32();
                        reader.ReadByte(); // flags
                        reader.ReadByte(); // variant
                        reader.ReadByte(); // rotationMirroring
                    }
                    else
                    {
                        yamlTileId = version < 6 ? reader.ReadUInt16() : reader.ReadInt32();
                        reader.ReadByte(); // flags
                        reader.ReadByte(); // variant
                    }

                    if (!tileMap.TryGetValue(yamlTileId, out var tileDefName))
                        continue;

                    if (!_tileDefinitionManager.TryGetDefinition(tileDefName, out var tileDef))
                        continue;

                    if (tileDef.TileId == 0)
                        continue;

                    var worldPos = new Vector2i(chunkX * chunkSize + x, chunkY * chunkSize + y);
                    coordinateMap[worldPos] = tileDef.ID;
                }
            }
        }

        return coordinateMap;
    }

    private static HashSet<string> GetPrototypeIdSet(IEnumerable<EntProtoId> prototypes)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var proto in prototypes)
            set.Add(proto.Id);

        return set;
    }

    private List<(Vector2i Position, string PrototypeId)> ParseWallEntities(
        SequenceDataNode entitiesNode,
        int gridUid,
        RuinChunkConfigPrototype config)
    {
        var wallEntities = new List<(Vector2i, string)>();
        var wallPrototypes = GetPrototypeIdSet(config.WallPrototypes);

        foreach (var protoGroup in entitiesNode.Sequence)
        {
            if (protoGroup is not MappingDataNode protoGroupNode)
                continue;

            if (!protoGroupNode.TryGet("proto", out ValueDataNode? protoNode) || protoNode == null)
                continue;

            var protoId = protoNode.Value;
            if (!wallPrototypes.Contains(protoId))
                continue;

            if (!protoGroupNode.TryGet("entities", out SequenceDataNode? entitiesInGroup) || entitiesInGroup == null)
                continue;

            foreach (var entity in entitiesInGroup.Sequence)
            {
                if (entity is not MappingDataNode entityNode)
                    continue;

                if (TryParseTransformOnGrid(entityNode, gridUid, out var entityPos, out _, skipRotation: true))
                    wallEntities.Add((entityPos, protoId));
            }
        }

        return wallEntities;
    }

    private List<(Vector2i Position, string PrototypeId, Angle Rotation)> ParseWindowEntities(
        SequenceDataNode entitiesNode,
        int gridUid,
        RuinChunkConfigPrototype config)
    {
        var windowEntities = new List<(Vector2i, string, Angle)>();
        var windowPrototypes = GetPrototypeIdSet(config.WindowPrototypes);

        foreach (var protoGroup in entitiesNode.Sequence)
        {
            if (protoGroup is not MappingDataNode protoGroupNode)
                continue;

            if (!protoGroupNode.TryGet("proto", out ValueDataNode? protoNode) || protoNode == null)
                continue;

            var protoId = protoNode.Value;
            // Locked/department windoors share Windoor* / Firelock* prefixes; accept them when base types are allowlisted.
            if (!windowPrototypes.Contains(protoId) &&
                !MatchesWindowPrefix(protoId, windowPrototypes))
                continue;

            if (!protoGroupNode.TryGet("entities", out SequenceDataNode? entitiesInGroup) || entitiesInGroup == null)
                continue;

            foreach (var entity in entitiesInGroup.Sequence)
            {
                if (entity is not MappingDataNode entityNode)
                    continue;

                if (TryParseTransformOnGrid(entityNode, gridUid, out var entityPos, out var rotation, skipRotation: false))
                    windowEntities.Add((entityPos, protoId, rotation));
            }
        }

        return windowEntities;
    }

    private static bool TryParseTransformOnGrid(
        MappingDataNode entityNode,
        int gridUid,
        out Vector2i entityPos,
        out Angle rotation,
        bool skipRotation)
    {
        entityPos = default;
        rotation = Angle.Zero;

        // Skip the grid entity itself.
        if (entityNode.TryGet("uid", out ValueDataNode? uidNode) &&
            int.TryParse(uidNode.Value, out var entityUid) &&
            entityUid == gridUid)
        {
            return false;
        }

        if (!entityNode.TryGet("components", out SequenceDataNode? componentsNode) || componentsNode == null)
            return false;

        Vector2i? pos = null;
        int? parentUid = null;

        foreach (var node in componentsNode.Sequence)
        {
            if (node is not MappingDataNode componentNode)
                continue;

            if (!componentNode.TryGet("type", out ValueDataNode? typeNode) || typeNode?.Value != "Transform")
                continue;

            if (componentNode.TryGet("pos", out ValueDataNode? posNode) && posNode != null)
            {
                var posParts = posNode.Value.Split(',');
                if (posParts.Length == 2 &&
                    float.TryParse(posParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(posParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    pos = new Vector2i((int)Math.Floor(x), (int)Math.Floor(y));
                }
            }

            if (componentNode.TryGet("parent", out ValueDataNode? parentNode) &&
                parentNode != null &&
                int.TryParse(parentNode.Value, out var parent))
            {
                parentUid = parent;
            }

            if (!skipRotation &&
                componentNode.TryGet("rot", out ValueDataNode? rotNode) &&
                rotNode != null)
            {
                var rotStr = rotNode.Value.Trim();
                if (rotStr.EndsWith("rad", StringComparison.OrdinalIgnoreCase))
                    rotStr = rotStr[..^3].Trim();

                if (float.TryParse(rotStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var rotValue))
                    rotation = new Angle(rotValue);
            }
        }

        if (!pos.HasValue || parentUid != gridUid)
            return false;

        entityPos = pos.Value;
        return true;
    }

    private Dictionary<Vector2i, int> BuildCostMap(
        Dictionary<Vector2i, string> coordinateMap,
        List<(Vector2i Position, string PrototypeId, Angle Rotation)> windowEntities,
        List<(Vector2i Position, string PrototypeId)> wallEntities,
        RuinChunkConfigPrototype config)
    {
        var costMap = new Dictionary<Vector2i, int>();
        var windowsByPosition = new Dictionary<Vector2i, string>();
        foreach (var (pos, proto, _) in windowEntities)
            windowsByPosition[pos] = proto;

        var wallPositions = new HashSet<Vector2i>(wallEntities.Select(w => w.Position));
        var wallCost = config.WallCost;

        foreach (var (pos, tileId) in coordinateMap)
        {
            if (wallPositions.Contains(pos))
            {
                costMap[pos] = wallCost;
            }
            else if (windowsByPosition.TryGetValue(pos, out var windowProto))
            {
                costMap[pos] = GetWindowCost(windowProto, config);
            }
            else
            {
                costMap[pos] = GetTileCost(tileId, config);
            }
        }

        foreach (var (wallPos, _) in wallEntities)
        {
            if (!costMap.ContainsKey(wallPos))
                costMap[wallPos] = wallCost;
        }

        foreach (var (windowPos, windowProto, _) in windowEntities)
        {
            if (!costMap.ContainsKey(windowPos))
                costMap[windowPos] = GetWindowCost(windowProto, config);
        }

        return costMap;
    }

    private static int GetLongestSubstringCost(string id, Dictionary<string, int> costs, int defaultCost)
    {
        if (costs.Count == 0)
            return defaultCost;

        var lookup = id.ToLowerInvariant();
        var bestMatch = string.Empty;
        foreach (var (pattern, _) in costs)
        {
            if (pattern.Length > bestMatch.Length && lookup.Contains(pattern.ToLowerInvariant()))
                bestMatch = pattern;
        }

        if (bestMatch.Length > 0 && costs.TryGetValue(bestMatch, out var cost))
            return cost;

        return defaultCost;
    }

    private static int GetWindowCost(string prototypeId, RuinChunkConfigPrototype config)
    {
        return GetLongestSubstringCost(prototypeId, config.WindowCosts, config.DefaultWindowCost);
    }

    private static bool IsWallTile(string tileId, RuinChunkConfigPrototype config)
    {
        if (config.WallTileIds.Count == 0)
            return false;

        foreach (var wallTileId in config.WallTileIds)
        {
            if (tileId.Equals(wallTileId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static int GetTileCost(string tileId, RuinChunkConfigPrototype config)
    {
        if (IsWallTile(tileId, config))
            return config.WallCost;

        return GetLongestSubstringCost(tileId, config.TileCosts, config.DefaultTileCost);
    }

    private static bool MatchesWindowPrefix(string protoId, HashSet<string> windowPrototypes)
    {
        // Department-locked windoors / firelocks are many discrete IDs; match by family when base is allowlisted.
        if (protoId.StartsWith("Windoor", StringComparison.OrdinalIgnoreCase) &&
            windowPrototypes.Contains("Windoor"))
            return true;

        if (protoId.StartsWith("Firelock", StringComparison.OrdinalIgnoreCase) &&
            windowPrototypes.Contains("Firelock"))
            return true;

        return false;
    }

    #endregion
}
