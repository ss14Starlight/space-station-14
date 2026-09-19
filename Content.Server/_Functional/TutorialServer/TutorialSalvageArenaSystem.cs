using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gravity;
using Content.Server.Power.EntitySystems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Atmos.Components;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Builds a salvage bay + nearby debris grid for magnet/EVA/locker/recycler tutorials.
/// Layout: sealed suit-up foyer (west) → minifan doorway → open training bay (east) → lattice to debris.
/// </summary>
public sealed partial class TutorialSalvageArenaSystem : EntitySystem
{
    private static readonly EntProtoId MinifanProto = "AtmosDeviceFanTiny";

    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TileSystem _tile = default!;

    public bool TryBuildArena(
        ProtoId<TutorialSalvageArenaPrototype> arenaId,
        out EntityUid mapUid,
        out EntityUid bayUid,
        out EntityCoordinates spawnCoords)
    {
        mapUid = EntityUid.Invalid;
        bayUid = EntityUid.Invalid;
        spawnCoords = default;

        if (!_protos.TryIndex(arenaId, out TutorialSalvageArenaPrototype? arena))
        {
            Log.Error($"Unknown tutorialSalvageArena {arenaId}");
            return false;
        }

        mapUid = _map.CreateMap(out var mapId);
        bayUid = BuildBay(mapId);
        PrepareGrid(bayUid);

        var debris = BuildDebris(mapId);
        PrepareGrid(debris);
        _transform.SetMapCoordinates(debris, new MapCoordinates(arena.DebrisOffset, mapId));

        spawnCoords = ResolveSpawn(bayUid);
        Log.Info($"TUTORIAL_E2E: salvage_arena_ready bay={bayUid} debris={debris}");
        return true;
    }

    private void PrepareGrid(EntityUid gridUid)
    {
        // Power/atmos simplification is applied from TutorialRolePrototype.SimplifiedEnvironment
        // after load (salvage keeps live atmos for EVA / space exposure).

        var gravity = EnsureComp<GravityComponent>(gridUid);
        _gravity.EnableGravity(gridUid, gravity);
        gravity.Inherent = true;
        if (!gravity.Enabled)
            gravity.Enabled = true;
        Dirty(gridUid, gravity);

        EnsureComp<GridAtmosphereComponent>(gridUid);
        EnsureComp<GasTileOverlayComponent>(gridUid);
        if (TryComp<MapGridComponent>(gridUid, out var gridComp))
            _atmos.RebuildGridAtmosphere((gridUid, Comp<GridAtmosphereComponent>(gridUid), gridComp));
    }

    private EntityUid BuildBay(MapId mapId)
    {
        // West foyer (0..4) sealed suit-up; doorway at x=4; main bay (5..12); lattice 10..12 east.
        const int w = 13;
        const int h = 11;
        const int foyerMaxX = 4;
        const int doorwayY = 5;

        var grid = _map.CreateGridEntity(mapId);
        var gridUid = grid.Owner;
        var floor = (ContentTileDefinition) _tiles["FloorSteel"];
        var lattice = (ContentTileDefinition) _tiles["Lattice"];
        var tiles = new List<(Vector2i, Tile)>();

        for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
        {
            var useLattice = x >= w - 3;
            tiles.Add((new Vector2i(x, y), _tile.GetVariantTile(useLattice ? lattice : floor, new Random()))); // Starlight edit
        }

        _map.SetTiles(gridUid, grid.Comp, tiles);

        for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
        {
            var perimeter = x == 0 || y == 0 || x == w - 1 || y == h - 1;
            var foyerWall = x == foyerMaxX && y != doorwayY;
            if (!perimeter && !foyerWall)
                continue;

            // East lattice opening toward debris
            if (x == w - 1 && y is >= 4 and <= 6)
                continue;

            // Minifan doorway between foyer and bay (no solid wall).
            if (x == foyerMaxX && y == doorwayY)
                continue;

            SpawnAnchored("WallSolid", gridUid, new Vector2i(x, y));
        }

        // Suit-up foyer equipment (pressurized room west of minifan).
        SpawnAnchored("LockerSalvageSpecialistFilled", gridUid, new Vector2i(1, 3));
        SpawnAnchored("ClothingShoesBootsMag", gridUid, new Vector2i(2, 3));
        SpawnAnchored("WeaponProtoKineticAccelerator", gridUid, new Vector2i(3, 3));
        SpawnAnchored("Pickaxe", gridUid, new Vector2i(2, 7));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(1, 9));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(3, 1));

        // Minifan in the foyer→bay doorway keeps air in the suit-up room.
        SpawnAnchored(MinifanProto, gridUid, new Vector2i(foyerMaxX, doorwayY));

        // Main training bay (east of foyer).
        SpawnAnchored("TutorialSalvageMagnet", gridUid, new Vector2i(7, 8));
        SpawnAnchored("TutorialRecycler", gridUid, new Vector2i(7, 6));
        SpawnAnchored("TutorialOreProcessor", gridUid, new Vector2i(8, 6));
        SpawnAnchored("OreBox", gridUid, new Vector2i(9, 7));
        SpawnAnchored("OreBag", gridUid, new Vector2i(8, 3));
        SpawnAnchored("ScrapSteel", gridUid, new Vector2i(7, 3));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(6, 9));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(9, 9));

        var bayMarker = Spawn("TutorialStepMarker", new EntityCoordinates(gridUid, new Vector2(7.5f, 5.5f)));
        var bayMarkerComp = EnsureComp<TutorialStepMarkerComponent>(bayMarker);
        bayMarkerComp.MarkerId = "salvage-bay";
        Dirty(bayMarker, bayMarkerComp);

        // Spawn inside the sealed foyer so players suit up before entering the open bay.
        var spawn = Spawn("SpawnPointLatejoin", new EntityCoordinates(gridUid, new Vector2(2.5f, 5.5f)));
        EnsureComp<TutorialSpawnPointComponent>(spawn);

        return gridUid;
    }

    private EntityUid BuildDebris(MapId mapId)
    {
        const int w = 9;
        const int h = 9;
        var grid = _map.CreateGridEntity(mapId);
        var gridUid = grid.Owner;
        var floor = (ContentTileDefinition) _tiles["FloorSteelDirty"];
        var tiles = new List<(Vector2i, Tile)>();

        for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
            tiles.Add((new Vector2i(x, y), _tile.GetVariantTile(floor, new Random()))); // Starlight edit

        _map.SetTiles(gridUid, grid.Comp, tiles);

        for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
        {
            var perimeter = x == 0 || y == 0 || x == w - 1 || y == h - 1;
            if (!perimeter)
                continue;
            if (x == 0 && y is >= 3 and <= 5)
                continue; // west opening toward bay
            SpawnAnchored("WallSolid", gridUid, new Vector2i(x, y));
        }

        SpawnAnchored("TutorialDebrisLocker", gridUid, new Vector2i(4, 5));
        SpawnAnchored("ScrapSteel", gridUid, new Vector2i(3, 3));
        SpawnAnchored("ScrapSteel", gridUid, new Vector2i(5, 3));
        SpawnAnchored("SteelOre1", gridUid, new Vector2i(4, 3));
        SpawnAnchored("GoldOre1", gridUid, new Vector2i(6, 4));
        SpawnAnchored("TrashBananaPeel", gridUid, new Vector2i(2, 4));

        var debrisMarker = Spawn("TutorialStepMarker", new EntityCoordinates(gridUid, new Vector2(4.5f, 4.5f)));
        var marker = EnsureComp<TutorialStepMarkerComponent>(debrisMarker);
        marker.MarkerId = "debris-pass";
        Dirty(debrisMarker, marker);

        return gridUid;
    }

    private EntityUid SpawnAnchored(EntProtoId proto, EntityUid gridUid, Vector2i tile)
    {
        var coords = new EntityCoordinates(gridUid, tile.X + 0.5f, tile.Y + 0.5f);
        var uid = Spawn(proto, coords);
        _power.SetNeedsPower(uid, false);
        return uid;
    }

    private EntityCoordinates ResolveSpawn(EntityUid bayUid)
    {
        var query = EntityQueryEnumerator<TutorialSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == bayUid)
                return xform.Coordinates;
        }

        return new EntityCoordinates(bayUid, new Vector2(2.5f, 5.5f));
    }
}
