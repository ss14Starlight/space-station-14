using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Gravity;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Doors.Components;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Crops an explicit AABB from a station map into a sealed tutorial section grid.
/// </summary>
public sealed partial class TutorialSectionCropSystem : EntitySystem
{
    private static readonly string[] DenyPrefixes =
    [
        "DefaultStationBeacon",
        "Telecomms",
        "ComputerComms",
        "ComputerCrewMonitoring",
        "CrewMonitoringServer",
        "ComputerStationMap",
        "ComputerRadar",
        "ComputerShuttle",
        "ComputerIFF",
        "ResearchAndDevelopmentServer",
        "ComputerResearchAndDevelopment",
        "ComputerCargo",
        "CargoRequestComputer",
        "ComputerId",
        "ComputerAlert",
        "Holopad",
        "StationAiCore",
        "StationAiBrain",
        "StationAiHeld",
        "PersonalAI",
        "CrateNPC",
        "MobHamster",
        "AirAlarm",
        "FireAlarm",
        "GasPipeSensor",
        "SurveillanceCameraSpeaker",
        "SurveillanceCameraRouter",
        "SurveillanceCameraWireless",
        // Salvage wreck hostiles / loot spawners (interim salvage section crops).
        "SalvageMobSpawner",
        "SpaceTickSpawner",
        "SpawnMobKangarooSalvage",
        "SpawnMobSpiderSalvage",
        "SpawnMobSpace",
        "RandomCargoCorpseSpawner",
        "SalvageMaterialCrateSpawner",
        "SalvageCanisterSpawner",
    ];

    private static readonly string[] DenyExact =
    [
        "CommunicationsConsole",
        "ComputerCommunications",
        "SurveillanceCameraMonitor",
        "ComputerTelevision",
        "FaxMachine",
    ];

    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private TileSystem _tile = default!;
    // [Dependency] private Robust.Shared.Random.IRobustRandom _random = default!;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    /// <summary>
    /// Crops a section into a new map/grid. Caller owns cleanup of <paramref name="mapUid"/>.
    /// </summary>
    public bool TryCrop(
        ProtoId<TutorialSectionCropPrototype> cropId,
        out EntityUid mapUid,
        out EntityUid gridUid)
    {
        mapUid = EntityUid.Invalid;
        gridUid = EntityUid.Invalid;

        if (!_protos.TryIndex(cropId, out TutorialSectionCropPrototype? crop))
        {
            Log.Error($"Unknown tutorialSectionCrop {cropId}");
            return false;
        }

        if (crop.Size.X < 5 || crop.Size.Y < 5)
        {
            Log.Error($"Crop {cropId} size too small: {crop.Size}");
            return false;
        }

        var opts = MapLoadOptions.Default with
        {
            DeserializationOptions = DeserializationOptions.Default with
            {
                InitializeMaps = true,
                LogOrphanedGrids = false,
            },
        };

        if (!_mapLoader.TryLoadGeneric(crop.SourceMap, out var result, opts) || result.Grids.Count == 0)
        {
            Log.Error($"Failed to load crop source {crop.SourceMap}");
            return false;
        }

        var srcGrid = result.Grids.First().Owner;
        if (!TryComp<MapGridComponent>(srcGrid, out var srcGridComp))
        {
            _mapLoader.Delete(result);
            return false;
        }

        var min = crop.Origin;
        var max = crop.Origin + crop.Size - new Vector2i(1, 1);

        mapUid = _map.CreateMap(out var mapId);
        gridUid = _map.CreateGridEntity(mapId);

        // Enable before Inherent — EnableGravity no-ops when Inherent is already true.
        var gravity = EnsureComp<GravityComponent>(gridUid);
        _gravity.EnableGravity(gridUid, gravity);
        gravity.Inherent = true;
        if (!gravity.Enabled)
            gravity.Enabled = true;
        Dirty(gridUid, gravity);

        var destGrid = Comp<MapGridComponent>(gridUid);
        CopyRegion(srcGrid, srcGridComp, min, max, gridUid, destGrid);
        SealPerimeter(gridUid, destGrid, crop.Size.X, crop.Size.Y);
        StripDenied(gridUid);
        EnsureFloorUnderInterior(gridUid, destGrid, crop.Size.X, crop.Size.Y);

        var center = new Vector2(crop.Size.X / 2f + 0.5f, crop.Size.Y / 2f + 0.5f);
        // Prefer a non-empty floor near center for markers.
        if (!TryFindFloorNear(gridUid, destGrid, crop.Size, ref center))
            Log.Warning($"Crop {cropId}: no floor tile found near center; markers may be off-grid");

        // MarkerBase / SpawnPoint prototypes already request anchoring on spawn.
        Spawn("TutorialZoneOrigin", new EntityCoordinates(gridUid, center));
        var spawn = Spawn("TutorialRoomSpawnPoint", new EntityCoordinates(gridUid, center));
        EnsureComp<TutorialSpawnPointComponent>(spawn);

        var template = EnsureComp<TutorialRoomTemplateComponent>(gridUid);
        template.GateDoor = crop.GateDoor;
        template.FillAtmosphere = true;
        EnsureComp<TutorialForcePowerGridComponent>(gridUid);

        // Second pass: catch contents spawned by filled lockers / delayed MapInit.
        StripDenied(gridUid);

        _mapLoader.Delete(result);
        return true;
    }

    /// <summary>
    /// Crops and writes YAML to the Resources tree (or <paramref name="resourcesRoot"/>).
    /// </summary>
    public bool TryCropAndSave(
        ProtoId<TutorialSectionCropPrototype> cropId,
        string? resourcesRoot = null)
    {
        if (!_protos.TryIndex(cropId, out TutorialSectionCropPrototype? crop))
            return false;

        if (!TryCrop(cropId, out var mapUid, out var gridUid))
            return false;

        try
        {
            var root = resourcesRoot ?? FindResourcesDirectory();
            if (root == null)
            {
                Log.Error("Could not locate Resources/ directory for crop save");
                return false;
            }

            // ResPath like /Maps/_Functional/... → Resources/Maps/_Functional/...
            var relative = crop.Output.ToString().TrimStart('/');
            var fsPath = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fsPath)!);

            using var writer = new StreamWriter(fsPath, false, Encoding.UTF8);
            if (!_mapLoader.TrySaveGrid(gridUid, writer))
            {
                Log.Error($"TrySaveGrid failed for {cropId} → {fsPath}");
                return false;
            }

            Log.Info($"Saved tutorial section crop {cropId} → {fsPath}");
            return true;
        }
        finally
        {
            QueueDel(mapUid);
        }
    }

    public bool TryCropAndSaveAll(string? resourcesRoot = null, List<string>? failures = null)
    {
        var ok = true;
        foreach (var crop in _protos.EnumeratePrototypes<TutorialSectionCropPrototype>())
        {
            if (TryCropAndSave(crop.ID, resourcesRoot))
                continue;

            ok = false;
            failures?.Add(crop.ID);
            Log.Error($"Failed crop {crop.ID}");
        }

        return ok;
    }

    public static string? FindResourcesDirectory()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Resources", "Maps");
            if (Directory.Exists(candidate))
                return Path.Combine(dir.FullName, "Resources");
        }

        // Integration tests often run from bin/...
        dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Resources", "Maps");
            if (Directory.Exists(candidate))
                return Path.Combine(dir.FullName, "Resources");
        }

        return null;
    }

    public static bool IsDeniedPrototype(string? protoId)
    {
        if (string.IsNullOrEmpty(protoId))
            return false;

        // Circuitboards / stock parts / machine electronics in lockers are fine for practice kits.
        if (protoId.Contains("Circuitboard", StringComparison.Ordinal) ||
            protoId.Contains("StockPart", StringComparison.Ordinal) ||
            protoId.Contains("Electronics", StringComparison.Ordinal))
            return false;

        foreach (var exact in DenyExact)
        {
            if (protoId.Equals(exact, StringComparison.Ordinal))
                return true;
        }

        foreach (var prefix in DenyPrefixes)
        {
            if (protoId.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void CopyRegion(
        EntityUid srcGrid,
        MapGridComponent srcGridComp,
        Vector2i min,
        Vector2i max,
        EntityUid destGrid,
        MapGridComponent destGridComp)
    {
        var tiles = new List<(Vector2i Index, Tile Tile)>();
        for (var x = min.X; x <= max.X; x++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                var srcIdx = new Vector2i(x, y);
                var tileRef = _map.GetTileRef(srcGrid, srcGridComp, srcIdx);
                if (tileRef.Tile.IsEmpty)
                    continue;

                var destIdx = srcIdx - min;
                tiles.Add((destIdx, tileRef.Tile));
            }
        }

        // Ensure contiguous plating under the full AABB so stamp/split stays stable.
        var plating = (ContentTileDefinition) _tiles["Plating"];
        var fill = new List<(Vector2i Index, Tile Tile)>();
        var width = max.X - min.X + 1;
        var height = max.Y - min.Y + 1;
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
                fill.Add((new Vector2i(x, y), _tile.GetVariantTile(plating, new Random()))); // Starlight
        }

        _map.SetTiles(destGrid, destGridComp, fill);
        _map.SetTiles(destGrid, destGridComp, tiles);

        var minVec = new Vector2(min.X, min.Y);
        var toCopy = new List<(string ProtoId, Vector2 LocalPos, Angle Rotation, bool Anchored)>();
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var ent, out var xform))
        {
            if (ent == srcGrid || xform.GridUid != srcGrid)
                continue;

            if (!_metaQuery.TryGetComponent(ent, out var meta))
                continue;

            var protoId = meta.EntityPrototype?.ID;
            if (protoId == null || IsDeniedPrototype(protoId))
                continue;

            var tile = new Vector2i(
                (int) MathF.Floor(xform.LocalPosition.X),
                (int) MathF.Floor(xform.LocalPosition.Y));
            if (tile.X < min.X || tile.X > max.X || tile.Y < min.Y || tile.Y > max.Y)
                continue;

            toCopy.Add((protoId, xform.LocalPosition, xform.LocalRotation, xform.Anchored));
        }

        foreach (var (protoId, localPos, rotation, anchored) in toCopy)
        {
            var childPos = localPos - minVec;
            var spawned = Spawn(protoId, new EntityCoordinates(destGrid, childPos));
            _transform.SetLocalRotation(spawned, rotation);
            if (anchored)
            {
                var childXform = _xformQuery.GetComponent(spawned);
                if (!childXform.Anchored && childXform.GridUid == destGrid)
                    _transform.AnchorEntity((spawned, childXform), (destGrid, destGridComp));
            }
        }
    }

    private void SealPerimeter(EntityUid gridUid, MapGridComponent grid, int width, int height)
    {
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var onEdge = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                if (!onEdge)
                    continue;

                var indices = new Vector2i(x, y);
                foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), indices).ToArray())
                {
                    if (!_metaQuery.TryGetComponent(ent, out var meta))
                        continue;

                    var id = meta.EntityPrototype?.ID;
                    if (id == null)
                        continue;

                    if (id.Contains("Airlock", StringComparison.Ordinal) ||
                        id.Contains("Door", StringComparison.Ordinal) ||
                        id.Contains("Windoor", StringComparison.Ordinal) ||
                        id.Contains("Window", StringComparison.Ordinal) ||
                        id.Contains("Wall", StringComparison.Ordinal))
                    {
                        QueueDel(ent);
                    }
                }

                var useWindow = y == height - 1 && x > 0 && x < width - 1 && (x % 2 == 1);
                var proto = useWindow ? "Window" : "WallSolid";
                var coords = new EntityCoordinates(gridUid, x + 0.5f, y + 0.5f);
                Spawn(proto, coords);
            }
        }
    }

    private void StripDenied(EntityUid gridUid)
    {
        var toDelete = new List<EntityUid>();
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            if (!_metaQuery.TryGetComponent(uid, out var meta))
                continue;

            if (IsDeniedPrototype(meta.EntityPrototype?.ID))
                toDelete.Add(uid);
        }

        foreach (var uid in toDelete)
            QueueDel(uid);
    }

    private void EnsureFloorUnderInterior(EntityUid gridUid, MapGridComponent grid, int width, int height)
    {
        var steel = (ContentTileDefinition) _tiles["FloorSteel"];
        var updates = new List<(Vector2i Index, Tile Tile)>();
        for (var x = 1; x < width - 1; x++)
        {
            for (var y = 1; y < height - 1; y++)
            {
                var idx = new Vector2i(x, y);
                var tile = _map.GetTileRef(gridUid, grid, idx);
                if (!tile.Tile.IsEmpty)
                    continue;

                updates.Add((idx, _tile.GetVariantTile(steel, new Random()))); // Starlight edit
            }
        }

        if (updates.Count > 0)
            _map.SetTiles(gridUid, grid, updates);
    }

    private bool TryFindFloorNear(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i size,
        ref Vector2 center)
    {
        var cx = (int) MathF.Floor(center.X);
        var cy = (int) MathF.Floor(center.Y);
        for (var r = 0; r < Math.Max(size.X, size.Y); r++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                        continue;

                    var x = cx + dx;
                    var y = cy + dy;
                    if (x <= 0 || y <= 0 || x >= size.X - 1 || y >= size.Y - 1)
                        continue;

                    var indices = new Vector2i(x, y);
                    var tile = _map.GetTileRef(gridUid, grid, indices);
                    if (tile.Tile.IsEmpty)
                        continue;

                    // Prefer open floor — spawning on/next to airlocks clips players after vaulting.
                    if (TileHasDoorOrWall(gridUid, grid, indices))
                        continue;

                    center = new Vector2(x + 0.5f, y + 0.5f);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TileHasDoorOrWall(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), tile))
        {
            if (HasComp<DoorComponent>(ent))
                return true;

            if (!_metaQuery.TryGetComponent(ent, out var meta))
                continue;

            var id = meta.EntityPrototype?.ID;
            if (id != null &&
                (id.Contains("Wall", StringComparison.Ordinal) ||
                 id.Contains("Window", StringComparison.Ordinal)))
                return true;
        }

        return false;
    }
}
