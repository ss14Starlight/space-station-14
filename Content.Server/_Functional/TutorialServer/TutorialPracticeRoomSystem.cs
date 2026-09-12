using System.Collections.Generic;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gravity;
using Content.Server.Power.EntitySystems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Atmos.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Builds enclosed, lit multi-chamber department-styled practice suites for tutorial maps.
/// Inter-chamber doors stay bolted until <see cref="UnlockGatesForGoal"/> runs.
/// </summary>
public sealed partial class TutorialPracticeRoomSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    // [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    /// <summary>
    /// Creates a fresh map + multi-chamber grid from a <see cref="TutorialRoomPrototype"/>.
    /// Spawn coordinates are the center of chamber 0.
    /// </summary>
    public bool TryBuildRoom(
        ProtoId<TutorialRoomPrototype> roomId,
        out EntityUid mapUid,
        out EntityUid gridUid,
        out EntityCoordinates spawnCoords,
        int? chamberCount = null)
    {
        mapUid = EntityUid.Invalid;
        gridUid = EntityUid.Invalid;
        spawnCoords = default;

        if (!_protos.TryIndex(roomId, out TutorialRoomPrototype? room))
        {
            Log.Error($"Unknown tutorialRoom {roomId}");
            return false;
        }

        var chamberW = room.ResolveChamberWidth();
        var chamberH = room.ResolveChamberHeight();
        if (chamberW < 5 || chamberH < 5)
        {
            Log.Error($"tutorialRoom {roomId} chamber size is too small");
            return false;
        }

        var chambers = chamberCount ?? room.Chambers;
        chambers = Math.Clamp(chambers, 1, Math.Max(1, room.MaxChambers));

        mapUid = _map.CreateMap(out var mapId);
        gridUid = _map.CreateGridEntity(mapId);

        EnableInherentGravity(gridUid);

        var gridComp = Comp<MapGridComponent>(gridUid);
        var floorDef = (ContentTileDefinition) _tiles[room.FloorTile];
        ContentTileDefinition? altDef = null;
        if (!string.IsNullOrEmpty(room.AltFloorTile))
            altDef = (ContentTileDefinition) _tiles[room.AltFloorTile];

        var wallPad = room.ExposedToSpace ? 0 : 1;
        // Dividers sit between chambers: totalW = pad + n*W + (n-1) + pad
        var totalW = wallPad * 2 + chambers * chamberW + Math.Max(0, chambers - 1);
        var totalH = wallPad * 2 + chamberH;

        var tiles = new List<(Vector2i Index, Tile Tile)>(totalW * totalH);
        for (var x = 0; x < totalW; x++)
        {
            for (var y = 0; y < totalH; y++)
            {
                var def = floorDef;
                if (altDef != null && ((x + y) & 1) == 1)
                    def = altDef;

                if (room.ExposedToSpace && (x == 0 || y == 0 || x == totalW - 1 || y == totalH - 1))
                    def = (ContentTileDefinition) _tiles["Lattice"];

                tiles.Add((new Vector2i(x, y), _tile.GetVariantTile(def, new Random()))); // Starlight edit
            }
        }

        _map.SetTiles(gridUid, gridComp, tiles);

        var layout = EnsureComp<TutorialRoomLayoutComponent>(gridUid);
        layout.ChamberCenters.Clear();
        layout.GateDoors.Clear();

        for (var i = 0; i < chambers; i++)
        {
            var ox = wallPad + i * (chamberW + 1);
            var oy = wallPad;
            layout.ChamberCenters.Add(new Vector2(ox + chamberW / 2f + 0.5f, oy + chamberH / 2f + 0.5f));
        }

        Vector2i? exteriorDoor = null;
        if (!room.ExposedToSpace && room.DoorSide != null)
        {
            var fromEnd = Math.Clamp(room.DoorChamberFromEnd, 1, chambers);
            var exteriorChamber = chambers - fromEnd;
            exteriorDoor = ResolveExteriorDoorTile(
                room.DoorSide.Value,
                wallPad,
                chamberW,
                chamberH,
                exteriorChamber,
                totalW,
                totalH);
        }

        if (!room.ExposedToSpace)
        {
            BuildPerimeter(gridUid, room, totalW, totalH, exteriorDoor);
            BuildDividers(gridUid, room, wallPad, chamberW, chamberH, chambers, layout);
        }

        PlaceWallLights(gridUid, room, wallPad, chamberW, chamberH, chambers);
        PlaceFurniture(gridUid, room, layout);
        PlaceSpawnPoint(gridUid, layout.ChamberCenters[0]);
        EnsureGridSupport(gridUid);

        if (!room.ExposedToSpace && room.FillAtmosphere)
            FillRoomAtmosphere(gridUid);

        spawnCoords = new EntityCoordinates(gridUid, layout.ChamberCenters[0]);
        return true;
    }

    /// <summary>
    /// Brings every gate on the grid in line with <paramref name="goalIndex"/>: gates the player
    /// has earned are unbolted and opened, gates they have not are shut and bolted.
    /// </summary>
    /// <remarks>
    /// The bolting half is deliberately re-asserted on every goal advance rather than trusted to
    /// happen once at map init. It costs nothing when the gates are already correct, and it means
    /// no combination of map authoring, load order or a door left open in the editor can hand the
    /// player the next chamber early.
    /// </remarks>
    public void UnlockGatesForGoal(EntityUid gridUid, int goalIndex)
    {
        foreach (var doorUid in EnumerateGateDoors(gridUid))
        {
            if (!Exists(doorUid) || TerminatingOrDeleted(doorUid))
                continue;

            if (!TryComp<TutorialGateDoorComponent>(doorUid, out var gate))
                continue;

            // Crowbar-practice gates are never touched: closed, unbolted, opened only by prying.
            if (gate.RequirePry)
                continue;

            // Sub-goal gates open partway through a chamber; seal only, RefreshSubGoalGates opens.
            if (!string.IsNullOrEmpty(gate.UnlockAtSubGoalId))
            {
                if (!gate.Unlocked)
                    SealGate(doorUid);
                continue;
            }

            if (goalIndex < gate.UnlockAtGoalIndex)
            {
                if (!gate.Unlocked)
                    SealGate(doorUid);
                continue;
            }

            UnlockGate(doorUid, gate);
        }
    }

    /// <summary>
    /// Opens every gate keyed to a sub-goal the player has now reached. <paramref name="reached"/>
    /// is passed in because the curriculum lives in the rule system.
    /// </summary>
    public void RefreshSubGoalGates(EntityUid gridUid, Func<string, bool> reached)
    {
        foreach (var doorUid in EnumerateGateDoors(gridUid))
        {
            if (!Exists(doorUid) || TerminatingOrDeleted(doorUid))
                continue;

            if (!TryComp<TutorialGateDoorComponent>(doorUid, out var gate))
                continue;

            if (gate.Unlocked || gate.RequirePry || string.IsNullOrEmpty(gate.UnlockAtSubGoalId))
                continue;

            if (!reached(gate.UnlockAtSubGoalId))
                continue;

            UnlockGate(doorUid, gate);
        }
    }

    /// <summary>Unbolts and opens a gate, once.</summary>
    private void UnlockGate(EntityUid doorUid, TutorialGateDoorComponent gate)
    {
        if (gate.Unlocked)
            return;

        gate.Unlocked = true;
        Dirty(doorUid, gate);

        if (TryComp<DoorBoltComponent>(doorUid, out var bolt))
            _doors.SetBoltsDown((doorUid, bolt), false);

        _doors.TryOpen(doorUid);
    }

    /// <summary>
    /// Shuts and bolts a gate the player has not reached yet.
    /// </summary>
    public void SealGate(EntityUid doorUid)
    {
        if (TryComp<DoorComponent>(doorUid, out var door) && door.State != DoorState.Closed)
            _doors.TryClose(doorUid, door);

        if (!TryComp<DoorBoltComponent>(doorUid, out var bolt))
            return;

        if (bolt.BoltsDown)
        {
            // Already sealed from the prototype; the lights still need syncing, because nothing
            // has raised a state change on this door yet.
            _doors.UpdateBoltLightStatus((doorUid, bolt));
            return;
        }

        _doors.SetBoltsDown((doorUid, bolt), true);
    }

    /// <summary>
    /// Swaps the gate that unlocks at <paramref name="unlockAtGoal"/> for a crowbar-practice door.
    /// </summary>
    /// <remarks>
    /// Stamping copies one source chamber, so every divider gets the same gate prototype. Rather
    /// than thread a per-gate override through the whole stamp path, the one pry gate is converted
    /// afterwards — <see cref="SpawnGateDoor"/> then applies the unpowered/unbolted/never-auto-open
    /// treatment for us.
    /// </remarks>
    public bool TryConvertGateToPryDoor(EntityUid gridUid, int unlockAtGoal, EntProtoId pryProto)
    {
        if (!TryComp<TutorialRoomLayoutComponent>(gridUid, out var layout))
            return false;

        for (var i = 0; i < layout.GateDoors.Count; i++)
        {
            var existing = layout.GateDoors[i];
            if (!Exists(existing) || TerminatingOrDeleted(existing))
                continue;

            if (!TryComp<TutorialGateDoorComponent>(existing, out var gate) ||
                gate.UnlockAtGoalIndex != unlockAtGoal)
                continue;

            var tile = _map.TileIndicesFor(gridUid, Comp<MapGridComponent>(gridUid), Transform(existing).Coordinates);
            // Immediate, not queued: the replacement goes in the same tile this tick, and a
            // queued-delete door would linger in the wall and in gate lookups until end of tick.
            Del(existing);

            layout.GateDoors[i] = SpawnGateDoor(pryProto, gridUid, tile, unlockAtGoal);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gate doors on this grid. Runtime-stamped suites record them on the grid as they are built;
    /// hand-authored maps have no layout component, so their mapped gates are found by query.
    /// </summary>
    private IEnumerable<EntityUid> EnumerateGateDoors(EntityUid gridUid)
    {
        if (TryComp<TutorialRoomLayoutComponent>(gridUid, out var layout))
            return layout.GateDoors;

        var doors = new List<EntityUid>();
        var query = EntityQueryEnumerator<TutorialGateDoorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == gridUid)
                doors.Add(uid);
        }

        return doors;
    }

    /// <summary>
    /// Resolves spawn coordinates for a practice entity in a given chamber.
    /// </summary>
    public EntityCoordinates GetChamberCoords(EntityUid gridUid, int roomIndex, Vector2 offset)
    {
        if (!TryComp<TutorialRoomLayoutComponent>(gridUid, out var layout) || layout.ChamberCenters.Count == 0)
            return new EntityCoordinates(gridUid, offset);

        var idx = Math.Clamp(roomIndex, 0, layout.ChamberCenters.Count - 1);
        return new EntityCoordinates(gridUid, layout.ChamberCenters[idx] + offset);
    }

    private Vector2i ResolveExteriorDoorTile(
        TutorialRoomDoorSide side,
        int wallPad,
        int chamberW,
        int chamberH,
        int chamberIndex,
        int totalW,
        int totalH)
    {
        var ox = wallPad + chamberIndex * (chamberW + 1);
        var midY = wallPad + chamberH / 2;
        var midX = ox + chamberW / 2;

        // East/west exterior doors sit on the suite perimeter above that chamber's y-mid.
        return side switch
        {
            TutorialRoomDoorSide.North => new Vector2i(midX, totalH - 1),
            TutorialRoomDoorSide.South => new Vector2i(midX, 0),
            TutorialRoomDoorSide.East => new Vector2i(totalW - 1, midY),
            TutorialRoomDoorSide.West => new Vector2i(0, midY),
            _ => new Vector2i(totalW - 1, midY),
        };
    }

    private void BuildPerimeter(
        EntityUid gridUid,
        TutorialRoomPrototype room,
        int totalW,
        int totalH,
        Vector2i? exteriorDoor)
    {
        for (var x = 0; x < totalW; x++)
        {
            for (var y = 0; y < totalH; y++)
            {
                var onPerimeter = x == 0 || y == 0 || x == totalW - 1 || y == totalH - 1;
                if (!onPerimeter)
                    continue;

                var indices = new Vector2i(x, y);
                if (exteriorDoor != null && indices == exteriorDoor)
                {
                    SpawnAnchored(room.Door, gridUid, indices);
                    // Crowbar-practice airlocks stay unpowered; all other exterior doors get an APC.
                    if (room.Door.Id != "TutorialAirlockMaint")
                        PowerDoorWithApc(gridUid, indices);
                    continue;
                }

                var useWindow = room.Windows && y == totalH - 1 && x > 0 && x < totalW - 1 && (x % 2 == 1);
                SpawnAnchored(useWindow ? room.Window : room.Wall, gridUid, indices);
            }
        }
    }

    private void BuildDividers(
        EntityUid gridUid,
        TutorialRoomPrototype room,
        int wallPad,
        int chamberW,
        int chamberH,
        int chambers,
        TutorialRoomLayoutComponent layout)
    {
        for (var i = 0; i < chambers - 1; i++)
        {
            var dividerX = wallPad + (i + 1) * chamberW + i;
            var doorY = wallPad + chamberH / 2;

            for (var y = wallPad; y < wallPad + chamberH; y++)
            {
                var indices = new Vector2i(dividerX, y);
                if (y == doorY)
                {
                    var door = SpawnGateDoor(room.GateDoor, gridUid, indices, unlockAtGoal: i + 1);
                    layout.GateDoors.Add(door);
                    continue;
                }

                SpawnAnchored(room.Wall, gridUid, indices);
            }
        }
    }

    private void PlaceWallLights(
        EntityUid gridUid,
        TutorialRoomPrototype room,
        int wallPad,
        int chamberW,
        int chamberH,
        int chambers)
    {
        var spacing = Math.Max(room.LightSpacing, 3);

        for (var i = 0; i < chambers; i++)
        {
            var ox = wallPad + i * (chamberW + 1);
            var oy = wallPad;
            // Interior tiles just south of the north wall — wallmount faces into the room.
            var lightY = oy + chamberH - 1;
            var startX = ox + 1;
            var endX = ox + chamberW - 2;

            if (endX < startX)
            {
                PlaceWallLight(room.Light, gridUid, new Vector2i(ox + chamberW / 2, lightY));
                continue;
            }

            for (var x = startX; x <= endX; x += spacing)
                PlaceWallLight(room.Light, gridUid, new Vector2i(x, lightY));

            // One light near the west edge if the spacing skipped it.
            if ((endX - startX) % spacing != 0)
                PlaceWallLight(room.Light, gridUid, new Vector2i(endX, lightY));
        }
    }

    private void PlaceWallLight(EntProtoId proto, EntityUid gridUid, Vector2i tile)
    {
        // Face south into the chamber (wallmounts hang on the north wall).
        PlaceWallLight(proto, gridUid, tile, Angle.FromDegrees(180));
    }

    private void PlaceWallLight(EntProtoId proto, EntityUid gridUid, Vector2i tile, Angle rotation)
    {
        var uid = SpawnAnchored(proto, gridUid, tile);
        _xform.SetLocalRotation(uid, rotation);
        _power.SetNeedsPower(uid, false);
    }

    /// <summary>
    /// Adds always-powered wall lights around each stamped chamber AABB so section crops
    /// (which keep station fixtures that may stay dark) stay playable.
    /// </summary>
    public void PlaceChamberPerimeterLights(
        EntityUid gridUid,
        IReadOnlyList<Vector2i> chamberOrigins,
        int chamberW,
        int chamberH,
        EntProtoId? light = null,
        int spacing = 4,
        float lightFacingOffsetDegrees = 0f)
    {
        var proto = light ?? new EntProtoId("AlwaysPoweredWallLight");
        spacing = Math.Max(spacing, 3);

        // PointLight offset is local (0, -0.5); rotate so the glow faces into the room.
        var offset = lightFacingOffsetDegrees;
        var faceSouth = Angle.FromDegrees(180 + offset); // north wall
        var faceNorth = Angle.FromDegrees(0 + offset); // south wall
        var faceEast = Angle.FromDegrees(270 + offset); // west wall
        var faceWest = Angle.FromDegrees(90 + offset); // east wall

        foreach (var origin in chamberOrigins)
        {
            var minX = origin.X + 1;
            var maxX = origin.X + chamberW - 2;
            var minY = origin.Y + 1;
            var maxY = origin.Y + chamberH - 2;

            if (maxX < minX || maxY < minY)
                continue;

            // North + south walls
            for (var x = minX; x <= maxX; x += spacing)
            {
                PlaceWallLight(proto, gridUid, new Vector2i(x, maxY), faceSouth);
                PlaceWallLight(proto, gridUid, new Vector2i(x, minY), faceNorth);
            }

            if ((maxX - minX) % spacing != 0)
            {
                PlaceWallLight(proto, gridUid, new Vector2i(maxX, maxY), faceSouth);
                PlaceWallLight(proto, gridUid, new Vector2i(maxX, minY), faceNorth);
            }

            // West + east walls (skip corners already covered)
            for (var y = minY + spacing; y <= maxY - spacing; y += spacing)
            {
                PlaceWallLight(proto, gridUid, new Vector2i(minX, y), faceEast);
                PlaceWallLight(proto, gridUid, new Vector2i(maxX, y), faceWest);
            }
        }
    }

    private void PlaceFurniture(EntityUid gridUid, TutorialRoomPrototype room, TutorialRoomLayoutComponent layout)
    {
        if (layout.ChamberCenters.Count == 0)
            return;

        foreach (var furn in room.Furniture)
        {
            var idx = Math.Clamp(furn.Room, 0, layout.ChamberCenters.Count - 1);
            var coords = new EntityCoordinates(gridUid, layout.ChamberCenters[idx] + furn.Offset);
            var ent = Spawn(furn.Id, coords);
            _power.SetNeedsPower(ent, false);
        }
    }

    private void PlaceSpawnPoint(EntityUid gridUid, Vector2 center)
    {
        var coords = new EntityCoordinates(gridUid, center);
        var spawn = Spawn("SpawnPointLatejoin", coords);
        EnsureComp<TutorialSpawnPointComponent>(spawn);
    }

    private EntityUid SpawnAnchored(EntProtoId proto, EntityUid gridUid, Vector2i tile)
    {
        var coords = new EntityCoordinates(gridUid, tile.X + 0.5f, tile.Y + 0.5f);
        return Spawn(proto, coords);
    }

    /// <summary>
    /// Fills a grid with standard air if it has none, exactly as the fixgridatmos command does.
    /// </summary>
    /// <remarks>
    /// A grid saved straight out of the map editor carries an empty <c>GridAtmosphere</c>, so a
    /// hand-authored tutorial map is a vacuum unless the mapper remembered to run fixgridatmos
    /// before saving. Grids that already hold gas are left alone.
    /// </remarks>
    public void EnsureBreathableAtmosphere(EntityUid gridUid)
    {
        if (TryComp<GridAtmosphereComponent>(gridUid, out var atmos) && atmos.Tiles.Count > 0)
            return;

        FillRoomAtmosphere(gridUid);
    }

    private void FillRoomAtmosphere(EntityUid gridUid)
    {
        EnsureComp<GridAtmosphereComponent>(gridUid);
        EnsureComp<GasTileOverlayComponent>(gridUid);

        if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
            return;

        _atmos.RebuildGridAtmosphere((gridUid, Comp<GridAtmosphereComponent>(gridUid), gridComp));
    }
}
