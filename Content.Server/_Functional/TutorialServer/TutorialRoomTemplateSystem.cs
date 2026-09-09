using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gravity;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Atmos.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Loads a single-room tutorial template and stamps N identical copies along a
/// configurable axis with bolted gate doors between them (one copy per curriculum goal stage).
/// </summary>
public sealed partial class TutorialRoomTemplateSystem : EntitySystem
{
    private static readonly EntProtoId DefaultGateDoor = "Airlock";

    /// <summary>
    /// Chambers a stamped suite gets when nothing says otherwise. Room prototypes raise it with
    /// <see cref="TutorialRoomPrototype.MaxChambers"/>; map-crop templates have no prototype to ask,
    /// so they keep the original ceiling.
    /// </summary>
    private const int DefaultMaxCopies = 8;

    [Dependency] private AtmosphereSystem _atmos = default!;
    // [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private IResourceManager _resources = default!;
    // [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private TutorialPracticeRoomSystem _rooms = default!;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    /// <summary>
    /// Resolves a <see cref="TutorialRoomTemplatePrototype"/> into a stamped suite.
    /// Prefers the authored map crop; falls back to a procedural single chamber.
    /// </summary>
    /// <param name="practicePathTargets">
    /// Chamber-relative practice offsets whose walk paths must stay unvaulted
    /// (especially important for single-chamber stamps).
    /// </param>
    public bool TryBuildFromTemplate(
        ProtoId<TutorialRoomTemplatePrototype> templateId,
        int copyCount,
        out EntityUid mapUid,
        out EntityUid gridUid,
        out EntityCoordinates spawnCoords,
        IReadOnlyList<(int Room, Vector2 Offset)>? practicePathTargets = null)
    {
        mapUid = EntityUid.Invalid;
        gridUid = EntityUid.Invalid;
        spawnCoords = default;

        if (!_protos.TryIndex(templateId, out TutorialRoomTemplatePrototype? template))
        {
            Log.Error($"Unknown tutorialRoomTemplate {templateId}");
            return false;
        }

        copyCount = Math.Clamp(copyCount, 1, Math.Max(1, template.MaxCopies));

        if (template.Map is { } mapPath &&
            _resources.ContentFileExists(mapPath) &&
            TryLoadTemplateGrid(mapPath, out var srcMap, out var srcGrid))
        {
            var gate = template.GateDoor;
            var fillAtmos = template.FillAtmosphere;
            var stampDir = template.StampDirection;
            if (TryComp<TutorialRoomTemplateComponent>(srcGrid, out var marker))
            {
                if (!string.IsNullOrEmpty(marker.GateDoor.Id))
                    gate = marker.GateDoor;
                fillAtmos = marker.FillAtmosphere;
                if (marker.StampDirection is { } markerDir)
                    stampDir = markerDir;
            }

            var ok = TryStampCopies(srcGrid, copyCount, gate, fillAtmos, stampDir,
                out mapUid, out gridUid, out spawnCoords, practicePathTargets,
                template.LightFacingOffsetDegrees);
            QueueDel(srcMap);
            return ok;
        }

        if (template.FallbackRoom is { } fallback)
            return TryStampFromRoomPrototype(fallback, copyCount, template.GateDoor, template.FillAtmosphere,
                out mapUid, out gridUid, out spawnCoords, template.StampDirection, practicePathTargets,
                template.LightFacingOffsetDegrees);

        Log.Error($"tutorialRoomTemplate {templateId} has no loadable map and no fallbackRoom");
        return false;
    }

    /// <summary>
    /// Builds one procedural chamber, then stamps it into <paramref name="copyCount"/> copies.
    /// </summary>
    public bool TryStampFromRoomPrototype(
        ProtoId<TutorialRoomPrototype> roomId,
        int copyCount,
        EntProtoId? gateDoor,
        bool fillAtmosphere,
        out EntityUid mapUid,
        out EntityUid gridUid,
        out EntityCoordinates spawnCoords,
        TutorialRoomDoorSide? stampDirection = null,
        IReadOnlyList<(int Room, Vector2 Offset)>? practicePathTargets = null,
        float lightFacingOffsetDegrees = 0f)
    {
        mapUid = EntityUid.Invalid;
        gridUid = EntityUid.Invalid;
        spawnCoords = default;

        if (!_rooms.TryBuildRoom(roomId, out var srcMap, out var srcGrid, out _, chamberCount: 1))
            return false;

        var gate = gateDoor ?? DefaultGateDoor;
        var maxCopies = DefaultMaxCopies;
        if (_protos.TryIndex(roomId, out TutorialRoomPrototype? room))
        {
            gate = gateDoor ?? room.GateDoor;
            // The room's own ceiling, so a curriculum longer than the default can raise it without
            // raising it for every suite in the game.
            maxCopies = Math.Max(1, room.MaxChambers);
        }

        // DoorSide on room protos is the exterior practice door, not the inter-chamber stamp axis.
        var stampDir = stampDirection ?? TutorialRoomDoorSide.East;

        var ok = TryStampCopies(srcGrid, copyCount, gate, fillAtmosphere, stampDir,
            out mapUid, out gridUid, out spawnCoords, practicePathTargets, lightFacingOffsetDegrees,
            maxCopies);
        QueueDel(srcMap);

        // Every divider gets the same gate from the stamp; convert the one crowbar-practice gate.
        if (ok && room?.PryGateAtGoalIndex is { } pryGoal)
            _rooms.TryConvertGateToPryDoor(gridUid, pryGoal, room.PryGateDoor);

        return ok;
    }

    /// <summary>
    /// Stamps <paramref name="copyCount"/> identical copies of <paramref name="templateGrid"/>
    /// onto a fresh map, with bolted gates between copies along <paramref name="stampDirection"/>.
    /// </summary>
    public bool TryStampCopies(
        EntityUid templateGrid,
        int copyCount,
        EntProtoId gateDoor,
        bool fillAtmosphere,
        out EntityUid mapUid,
        out EntityUid gridUid,
        out EntityCoordinates spawnCoords,
        IReadOnlyList<(int Room, Vector2 Offset)>? practicePathTargets = null,
        float lightFacingOffsetDegrees = 0f)
    {
        return TryStampCopies(templateGrid, copyCount, gateDoor, fillAtmosphere, TutorialRoomDoorSide.East,
            out mapUid, out gridUid, out spawnCoords, practicePathTargets, lightFacingOffsetDegrees);
    }

    /// <summary>
    /// Stamps <paramref name="copyCount"/> identical copies of <paramref name="templateGrid"/>
    /// onto a fresh map, with bolted gates between copies along <paramref name="stampDirection"/>.
    /// </summary>
    public bool TryStampCopies(
        EntityUid templateGrid,
        int copyCount,
        EntProtoId gateDoor,
        bool fillAtmosphere,
        TutorialRoomDoorSide stampDirection,
        out EntityUid mapUid,
        out EntityUid gridUid,
        out EntityCoordinates spawnCoords,
        IReadOnlyList<(int Room, Vector2 Offset)>? practicePathTargets = null,
        float lightFacingOffsetDegrees = 0f,
        int maxCopies = DefaultMaxCopies)
    {
        mapUid = EntityUid.Invalid;
        gridUid = EntityUid.Invalid;
        spawnCoords = default;

        if (!TryComp<MapGridComponent>(templateGrid, out var srcGridComp))
        {
            Log.Error("Tutorial room template is missing MapGridComponent");
            return false;
        }

        // Silently sharing the last chamber between the goals that did not fit is worse than it
        // sounds: their props all land on top of each other in it. Say so.
        if (copyCount > maxCopies)
        {
            Log.Warning(
                $"Tutorial stamp: curriculum asked for {copyCount} chambers, ceiling is {maxCopies}; " +
                "the last chambers will share a room. Raise maxChambers on the tutorialRoom.");
        }

        copyCount = Math.Clamp(copyCount, 1, maxCopies);

        if (!TryGetTileBounds(templateGrid, srcGridComp, out var min, out var max))
        {
            Log.Error("Tutorial room template has no tiles");
            return false;
        }

        var width = max.X - min.X + 1;
        var height = max.Y - min.Y + 1;
        if (width < 3 || height < 3)
        {
            Log.Error($"Tutorial room template bounds too small ({width}x{height})");
            return false;
        }

        var zoneLocal = ResolveZoneOrigin(templateGrid, min, max);
        var gateLateral = ResolveGateLateral(templateGrid, min, max, zoneLocal, stampDirection, width, height);

        mapUid = _map.CreateMap(out var mapId);
        gridUid = _map.CreateGridEntity(mapId);

        _rooms.EnableInherentGravity(gridUid);

        var destGrid = Comp<MapGridComponent>(gridUid);
        var layout = EnsureComp<TutorialRoomLayoutComponent>(gridUid);
        layout.ChamberCenters.Clear();
        layout.GateDoors.Clear();

        // Fill the full suite AABB with plating so irregular crops stay one contiguous grid
        // (GridFixtureSystem otherwise splits stamped copies into separate grids).
        GetSuiteSize(width, height, copyCount, stampDirection, out var totalW, out var totalH);
        var plating = (ContentTileDefinition) _tiles["Plating"];
        var fill = new List<(Vector2i Index, Tile Tile)>(totalW * totalH);
        for (var x = 0; x < totalW; x++)
        {
            for (var y = 0; y < totalH; y++)
                fill.Add((new Vector2i(x, y), _tile.GetVariantTile(plating, new Random()))); // Starlight edit
        }

        _map.SetTiles(gridUid, destGrid, fill);

        var chamberOrigins = new List<Vector2i>(copyCount);
        for (var i = 0; i < copyCount; i++)
        {
            var origin = GetChamberOrigin(i, width, height, copyCount, stampDirection);
            chamberOrigins.Add(origin);
            CopyTemplate(templateGrid, srcGridComp, min, max, gridUid, destGrid, origin);

            if (i < copyCount - 1)
                PunchOpening(gridUid, destGrid, GetForwardOpening(origin, width, height, stampDirection, gateLateral));
            if (i > 0)
                PunchOpening(gridUid, destGrid, GetBackwardOpening(origin, width, height, stampDirection, gateLateral));

            layout.ChamberCenters.Add(zoneLocal + origin);
        }

        for (var i = 0; i < copyCount - 1; i++)
        {
            PlaceDivider(gridUid, destGrid, width, height, copyCount, i, stampDirection, gateLateral,
                gateDoor, unlockAtGoal: i + 1, layout);
        }

        // Crop templates keep station airlocks; only the divider gate should be usable.
        // Practice offsets keep single-chamber kit paths open (R&D, bar, etc.).
        SealSuperfluousDoorsAndEnsurePaths(gridUid, destGrid, width, height, copyCount, stampDirection,
            gateLateral, layout, practicePathTargets);

        // Station crop fixtures are AP-powered and often stay dark; guarantee wall lights.
        _rooms.PlaceChamberPerimeterLights(gridUid, chamberOrigins, width, height,
            lightFacingOffsetDegrees: lightFacingOffsetDegrees);

        PlaceSpawnPoint(gridUid, layout.ChamberCenters[0]);
        _rooms.EnsureGridSupport(gridUid);

        if (fillAtmosphere)
            FillAtmosphere(gridUid);

        // Force-power APC receivers on stamped machines (crop may include unpowered devices).
        EnsureComp<TutorialForcePowerGridComponent>(gridUid);

        spawnCoords = new EntityCoordinates(gridUid, layout.ChamberCenters[0]);
        return true;
    }

    private static void GetSuiteSize(
        int width,
        int height,
        int copyCount,
        TutorialRoomDoorSide stampDirection,
        out int totalW,
        out int totalH)
    {
        var gaps = Math.Max(0, copyCount - 1);
        switch (stampDirection)
        {
            case TutorialRoomDoorSide.North:
            case TutorialRoomDoorSide.South:
                totalW = width;
                totalH = copyCount * height + gaps;
                break;
            default:
                totalW = copyCount * width + gaps;
                totalH = height;
                break;
        }
    }

    /// <summary>
    /// Bottom-left origin of chamber <paramref name="index"/>. Next chamber lies in
    /// <paramref name="stampDirection"/> from the previous one.
    /// </summary>
    private static Vector2i GetChamberOrigin(
        int index,
        int width,
        int height,
        int copyCount,
        TutorialRoomDoorSide stampDirection)
    {
        return stampDirection switch
        {
            // Chamber 0 northmost; later copies step south (−Y).
            TutorialRoomDoorSide.South => new Vector2i(0, (copyCount - 1 - index) * (height + 1)),
            // Chamber 0 southmost; later copies step north (+Y).
            TutorialRoomDoorSide.North => new Vector2i(0, index * (height + 1)),
            // Chamber 0 eastmost; later copies step west (−X).
            TutorialRoomDoorSide.West => new Vector2i((copyCount - 1 - index) * (width + 1), 0),
            // Default: chamber 0 westmost; later copies step east (+X).
            _ => new Vector2i(index * (width + 1), 0),
        };
    }

    private static Vector2i GetForwardOpening(
        Vector2i origin,
        int width,
        int height,
        TutorialRoomDoorSide stampDirection,
        int gateLateral)
    {
        return stampDirection switch
        {
            TutorialRoomDoorSide.South => origin + new Vector2i(gateLateral, 0),
            TutorialRoomDoorSide.North => origin + new Vector2i(gateLateral, height - 1),
            TutorialRoomDoorSide.West => origin + new Vector2i(0, gateLateral),
            _ => origin + new Vector2i(width - 1, gateLateral),
        };
    }

    private static Vector2i GetBackwardOpening(
        Vector2i origin,
        int width,
        int height,
        TutorialRoomDoorSide stampDirection,
        int gateLateral)
    {
        // Opening that faces the previous chamber (opposite of stamp direction).
        return stampDirection switch
        {
            TutorialRoomDoorSide.South => origin + new Vector2i(gateLateral, height - 1),
            TutorialRoomDoorSide.North => origin + new Vector2i(gateLateral, 0),
            TutorialRoomDoorSide.West => origin + new Vector2i(width - 1, gateLateral),
            _ => origin + new Vector2i(0, gateLateral),
        };
    }

    /// <summary>
    /// Lateral gate coordinate on the stamp-forward edge: prefer an existing crop airlock
    /// on that edge near the zone origin, else the zone projection.
    /// </summary>
    private int ResolveGateLateral(
        EntityUid templateGrid,
        Vector2i min,
        Vector2i max,
        Vector2 zoneLocal,
        TutorialRoomDoorSide stampDirection,
        int width,
        int height)
    {
        var alongEdge = stampDirection is TutorialRoomDoorSide.North or TutorialRoomDoorSide.South;
        var maxLateral = (alongEdge ? width : height) - 1;
        var preferred = alongEdge
            ? (int) MathF.Floor(zoneLocal.X)
            : (int) MathF.Floor(zoneLocal.Y);
        preferred = Math.Clamp(preferred, 1, Math.Max(1, maxLateral - 1));

        var best = preferred;
        var bestDist = int.MaxValue;

        var query = EntityQueryEnumerator<DoorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != templateGrid)
                continue;

            // Skip high-security side offices — they must never become the tutorial path.
            if (_metaQuery.TryGetComponent(uid, out var meta) &&
                meta.EntityPrototype?.ID is { } protoId &&
                IsHeadOfficeDoorProto(protoId))
                continue;

            var tile = new Vector2i(
                (int) MathF.Floor(xform.LocalPosition.X),
                (int) MathF.Floor(xform.LocalPosition.Y));

            var onEdge = stampDirection switch
            {
                TutorialRoomDoorSide.South => tile.Y == min.Y,
                TutorialRoomDoorSide.North => tile.Y == max.Y,
                TutorialRoomDoorSide.West => tile.X == min.X,
                _ => tile.X == max.X,
            };
            if (!onEdge)
                continue;

            var lateral = alongEdge ? tile.X - min.X : tile.Y - min.Y;
            if (lateral <= 0 || lateral >= maxLateral)
                continue;

            var dist = Math.Abs(lateral - preferred);
            if (dist >= bestDist)
                continue;

            bestDist = dist;
            best = lateral;
        }

        return best;
    }

    private static bool IsHeadOfficeDoorProto(string protoId)
    {
        return protoId.Contains("ResearchDirector", StringComparison.Ordinal)
               || protoId.Contains("Captain", StringComparison.Ordinal)
               || protoId.Contains("ChiefMedical", StringComparison.Ordinal)
               || protoId.Contains("ChiefEngineer", StringComparison.Ordinal)
               || protoId.Contains("HeadOfPersonnel", StringComparison.Ordinal)
               || protoId.Contains("HighSec", StringComparison.Ordinal)
               || protoId.Contains("Armory", StringComparison.Ordinal);
    }

    private void PlaceDivider(
        EntityUid gridUid,
        MapGridComponent grid,
        int width,
        int height,
        int copyCount,
        int gateIndex,
        TutorialRoomDoorSide stampDirection,
        int gateLateral,
        EntProtoId gateDoor,
        int unlockAtGoal,
        TutorialRoomLayoutComponent layout)
    {
        var origin = GetChamberOrigin(gateIndex, width, height, copyCount, stampDirection);
        switch (stampDirection)
        {
            case TutorialRoomDoorSide.South:
                // Divider just south of chamber gateIndex.
                PlaceDividerRow(gridUid, grid, origin.Y - 1, width, gateLateral, gateDoor, unlockAtGoal, layout);
                break;
            case TutorialRoomDoorSide.North:
                // Divider just north of chamber gateIndex.
                PlaceDividerRow(gridUid, grid, origin.Y + height, width, gateLateral, gateDoor, unlockAtGoal, layout);
                break;
            case TutorialRoomDoorSide.West:
                PlaceDividerColumn(gridUid, grid, origin.X - 1, height, gateLateral, gateDoor, unlockAtGoal, layout);
                break;
            default:
                PlaceDividerColumn(gridUid, grid, origin.X + width, height, gateLateral, gateDoor, unlockAtGoal, layout);
                break;
        }
    }

    private bool TryLoadTemplateGrid(ResPath path, out EntityUid mapUid, out EntityUid gridUid)
    {
        mapUid = EntityUid.Invalid;
        gridUid = EntityUid.Invalid;

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        mapUid = _map.CreateMap(out var mapId);
        if (_mapLoader.TryLoadGrid(mapId, path, out var grid, opts))
        {
            gridUid = grid.Value.Owner;
            EnsureComp<TutorialRoomTemplateComponent>(gridUid);
            return true;
        }

        QueueDel(mapUid);
        mapUid = EntityUid.Invalid;

        if (!_mapLoader.TryLoadMap(path, out var map, out var grids, opts) ||
            map == null ||
            grids == null ||
            grids.Count == 0)
        {
            Log.Warning($"Failed to load tutorial room template map {path}");
            return false;
        }

        mapUid = map.Value.Owner;
        gridUid = grids.First().Owner;
        EnsureComp<TutorialRoomTemplateComponent>(gridUid);
        return true;
    }

    private bool TryGetTileBounds(
        EntityUid gridUid,
        MapGridComponent grid,
        out Vector2i min,
        out Vector2i max)
    {
        min = new Vector2i(int.MaxValue, int.MaxValue);
        max = new Vector2i(int.MinValue, int.MinValue);
        var found = false;

        foreach (var tile in _map.GetAllTiles(gridUid, grid))
        {
            if (tile.Tile.IsEmpty)
                continue;

            found = true;
            min = Vector2i.ComponentMin(min, tile.GridIndices);
            max = Vector2i.ComponentMax(max, tile.GridIndices);
        }

        return found;
    }

    private Vector2 ResolveZoneOrigin(EntityUid templateGrid, Vector2i min, Vector2i max)
    {
        var minVec = new Vector2(min.X, min.Y);

        var query = EntityQueryEnumerator<TutorialZoneOriginComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid != templateGrid)
                continue;
            // Convert to template-local coords relative to min corner used as stamp origin.
            return xform.LocalPosition - minVec;
        }

        var spawnQuery = EntityQueryEnumerator<TutorialSpawnPointComponent, TransformComponent>();
        while (spawnQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid != templateGrid)
                continue;
            return xform.LocalPosition - minVec;
        }

        return new Vector2(
            (max.X - min.X) / 2f + 0.5f,
            (max.Y - min.Y) / 2f + 0.5f);
    }

    private void CopyTemplate(
        EntityUid srcGrid,
        MapGridComponent srcGridComp,
        Vector2i min,
        Vector2i max,
        EntityUid destGrid,
        MapGridComponent destGridComp,
        Vector2i destOrigin)
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

                var destIdx = destOrigin + (srcIdx - min);
                tiles.Add((destIdx, tileRef.Tile));
            }
        }

        _map.SetTiles(destGrid, destGridComp, tiles);

        var minVec = new Vector2(min.X, min.Y);
        var destOriginVec = new Vector2(destOrigin.X, destOrigin.Y);

        // Snapshot first — spawning while enumerating transforms mutates the component store.
        var toCopy = new List<(string ProtoId, Vector2 LocalPos, Angle Rotation, bool Anchored)>();
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var ent, out var xform))
        {
            if (ent == srcGrid || xform.GridUid != srcGrid)
                continue;

            if (!_metaQuery.TryGetComponent(ent, out var meta))
                continue;

            // Markers / spawn points are recreated on the destination for copy 0 only.
            if (HasComp<TutorialSpawnPointComponent>(ent) ||
                HasComp<TutorialZoneOriginComponent>(ent) ||
                HasComp<TutorialRoomTemplateComponent>(ent))
                continue;

            var protoId = meta.EntityPrototype?.ID;
            if (protoId == null)
                continue;

            // Skip grid-support helpers; destination gets its own.
            if (protoId is "TutorialInvisibleGridSupport")
                continue;

            // Only copy entities whose anchor/position falls inside the template AABB.
            var tile = new Vector2i((int)MathF.Floor(xform.LocalPosition.X), (int)MathF.Floor(xform.LocalPosition.Y));
            if (tile.X < min.X || tile.X > max.X || tile.Y < min.Y || tile.Y > max.Y)
                continue;

            toCopy.Add((protoId, xform.LocalPosition, xform.LocalRotation, xform.Anchored));
        }

        foreach (var (protoId, localPos, rotation, anchored) in toCopy)
        {
            var childPos = localPos - minVec + destOriginVec;
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

    private void PunchOpening(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        // Immediate Del — SealSuperfluousDoorsAndEnsurePaths pathfinds in the same call;
        // QueueDel would leave walls/doors on the opening tile and fail the walk check.
        foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), tile).ToArray())
        {
            if (HasComp<TutorialGateDoorComponent>(ent))
                continue;
            Del(ent);
        }
    }

    private void PlaceDividerColumn(
        EntityUid gridUid,
        MapGridComponent grid,
        int dividerX,
        int height,
        int doorY,
        EntProtoId gateDoor,
        int unlockAtGoal,
        TutorialRoomLayoutComponent layout)
    {
        for (var y = 0; y < height; y++)
        {
            var indices = new Vector2i(dividerX, y);
            EnsureDividerFloor(gridUid, grid, indices, new Vector2i(-1, 0));

            foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), indices).ToArray())
                Del(ent);

            if (y == doorY)
            {
                var door = _rooms.SpawnGateDoorPublic(gateDoor, gridUid, indices, unlockAtGoal);
                layout.GateDoors.Add(door);
            }
            else
            {
                var coords = new EntityCoordinates(gridUid, indices.X + 0.5f, indices.Y + 0.5f);
                Spawn("WallSolid", coords);
            }
        }
    }

    private void PlaceDividerRow(
        EntityUid gridUid,
        MapGridComponent grid,
        int dividerY,
        int width,
        int doorX,
        EntProtoId gateDoor,
        int unlockAtGoal,
        TutorialRoomLayoutComponent layout)
    {
        for (var x = 0; x < width; x++)
        {
            var indices = new Vector2i(x, dividerY);
            EnsureDividerFloor(gridUid, grid, indices, new Vector2i(0, 1));

            foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), indices).ToArray())
                Del(ent);

            if (x == doorX)
            {
                var door = _rooms.SpawnGateDoorPublic(gateDoor, gridUid, indices, unlockAtGoal);
                layout.GateDoors.Add(door);
            }
            else
            {
                var coords = new EntityCoordinates(gridUid, indices.X + 0.5f, indices.Y + 0.5f);
                Spawn("WallSolid", coords);
            }
        }
    }

    private void EnsureDividerFloor(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i indices,
        Vector2i neighborOffset)
    {
        if (!_map.GetTileRef(gridUid, grid, indices).Tile.IsEmpty)
            return;

        var neighbor = _map.GetTileRef(gridUid, grid, indices + neighborOffset);
        if (!neighbor.Tile.IsEmpty)
            _map.SetTile(gridUid, grid, indices, neighbor.Tile);
    }

    private void PlaceSpawnPoint(EntityUid gridUid, Vector2 center)
    {
        var coords = new EntityCoordinates(gridUid, center);
        var spawn = Spawn("SpawnPointLatejoin", coords);
        EnsureComp<TutorialSpawnPointComponent>(spawn);
    }

    private void FillAtmosphere(EntityUid gridUid)
    {
        EnsureComp<GridAtmosphereComponent>(gridUid);
        EnsureComp<GasTileOverlayComponent>(gridUid);

        if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
            return;

        _atmos.RebuildGridAtmosphere((gridUid, Comp<GridAtmosphereComponent>(gridUid), gridComp));
    }
}
