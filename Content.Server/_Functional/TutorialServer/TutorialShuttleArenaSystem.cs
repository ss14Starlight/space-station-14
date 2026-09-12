using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Gravity;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Atmos.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Gravity;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Shuttles.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Builds a cargo-shuttle practice arena: real (or trainer) shuttle + cargo bay + ATS mini-stations.
/// </summary>
public sealed partial class TutorialShuttleArenaSystem : EntitySystem
{
    public const string CargoBayStationId = "cargo-bay";
    public const string AtsStationId = "ats";
    public const string CargoShuttleBoardMarkerId = "cargo-shuttle";

    private static readonly EntProtoId TutorialCargoTradeStationProto = "TutorialCargoTradeStation";
    private static readonly EntProtoId TutorialStepMarkerProto = "TutorialStepMarker";
    private static readonly ProtoId<TagPrototype> BayCrateTag = "TutorialCargoBayCrate";

    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private DockingSystem _docking = default!;
    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    // [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private TileSystem _tile = default!;

    private static readonly ProtoId<CargoBountyPrototype> TutorialBounty = "BountyBread";

    public bool TryBuildArena(
        ProtoId<TutorialShuttleArenaPrototype> arenaId,
        out EntityUid mapUid,
        out EntityUid shuttleUid,
        out EntityCoordinates spawnCoords)
    {
        mapUid = EntityUid.Invalid;
        shuttleUid = EntityUid.Invalid;
        spawnCoords = default;

        if (!_protos.TryIndex(arenaId, out TutorialShuttleArenaPrototype? arena))
        {
            Log.Error($"Unknown tutorialShuttleArena {arenaId}");
            return false;
        }

        mapUid = _map.CreateMap(out var mapId);

        if (arena.ShuttleMap != null)
        {
            if (!_loader.TryLoadGrid(mapId, arena.ShuttleMap.Value, out var loaded, DeserializationOptions.Default with { InitializeMaps = true })
                || loaded == null)
            {
                Log.Error($"Failed to load shuttle map {arena.ShuttleMap}");
                QueueDel(mapUid);
                mapUid = EntityUid.Invalid;
                return false;
            }

            shuttleUid = loaded.Value.Owner;
        }
        else
        {
            shuttleUid = BuildTrainerShuttle(mapId, arena);
        }

        EnsureComp<ShuttleComponent>(shuttleUid);
        if (arena.IncludeAtsSell)
            EnsureComp<CargoShuttleComponent>(shuttleUid);
        PrepareGrid(shuttleUid);
        // Cargo Tech spawns in the bay; other shuttle arenas keep a shuttle spawn.
        if (!arena.IncludeAtsSell)
            EnsureTutorialSpawnOnShuttle(shuttleUid);

        var homeDock = arena.IncludeAtsSell
            ? BuildCargoBayStation(mapId, arena)
            : BuildDockOnlyStation(mapId, arena, arena.HomeStationId, 15, 11);
        PrepareGrid(homeDock);
        _transform.SetMapCoordinates(homeDock, new MapCoordinates(arena.CargoBayOffset, mapId));

        var distantDock = arena.IncludeAtsSell
            ? BuildAtsStation(mapId, arena)
            : BuildDockOnlyStation(mapId, arena, arena.DistantStationId, 13, 9);
        PrepareGrid(distantDock);
        _transform.SetMapCoordinates(distantDock, new MapCoordinates(arena.AtsOffset, mapId));

        var docked = false;
        if (arena.StartDocked)
        {
            var config = _docking.GetDockingConfig(shuttleUid, homeDock);
            if (config == null && arena.FallbackShuttleMap != null)
            {
                Log.Warning($"Tutorial shuttle arena {arenaId}: primary shuttle failed dock config; trying fallback {arena.FallbackShuttleMap}");
                QueueDel(shuttleUid);
                if (_loader.TryLoadGrid(mapId, arena.FallbackShuttleMap.Value, out var fallback, DeserializationOptions.Default with { InitializeMaps = true })
                    && fallback != null)
                {
                    shuttleUid = fallback.Value.Owner;
                    EnsureComp<ShuttleComponent>(shuttleUid);
                    PrepareGrid(shuttleUid);
                    if (!arena.IncludeAtsSell)
                        EnsureTutorialSpawnOnShuttle(shuttleUid);
                    config = _docking.GetDockingConfig(shuttleUid, homeDock);
                }
            }

            if (config != null)
            {
                _shuttles.FTLDock((shuttleUid, Transform(shuttleUid)), config);
                docked = true;
                Log.Info($"TUTORIAL_E2E: shuttle_arena_docked shuttle={shuttleUid} homeDock={homeDock}");
            }
            else if (arena.IncludeAtsSell)
            {
                Log.Warning($"Tutorial shuttle arena {arenaId}: no docking config with home dock; starting undocked");
                _transform.SetMapCoordinates(shuttleUid, new MapCoordinates(arena.CargoBayOffset + new Vector2(-18f, 0f), mapId));
            }
            else
            {
                Log.Error($"Tutorial shuttle arena {arenaId}: docking required but GetDockingConfig failed");
                QueueDel(mapUid);
                mapUid = EntityUid.Invalid;
                shuttleUid = EntityUid.Invalid;
                return false;
            }
        }

        // Dock platforms must stay Static; the practice shuttle must be Dynamic or thrusters
        // cannot move it after undock (feels like "welds never released").
        // Re-enable the shuttle after freezing pads — changing a welded pad to Static can leave
        // the shuttle body Static in the same physics island until we force Dynamic again.
        MakeStaticDockPlatform(homeDock);
        MakeStaticDockPlatform(distantDock);
        EnsureShuttleFlyable(shuttleUid);

        if (arena.IncludeAtsSell)
        {
            EnsureTutorialSpawnOnCargoBay(homeDock);
            EnsureShuttleBoardMarker(shuttleUid);
            spawnCoords = ResolveGridSpawn(homeDock);
        }
        else
        {
            spawnCoords = ResolveShuttleSpawn(shuttleUid);
        }

        Log.Info($"TUTORIAL_E2E: shuttle_arena_ready docked={docked} shuttle={shuttleUid} home={homeDock} distant={distantDock}");
        return true;
    }

    private void PrepareGrid(EntityUid gridUid)
    {
        // Force-power is applied after load via TutorialMapSystem (shuttle arenas always).
        // TEMPORARY: SimplifiedEnvironment atmos freeze is globally off (odd behavior).

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

    /// <summary>
    /// <see cref="ShuttleSystem"/> enables every grid as a Dynamic shuttle on init. Tutorial dock
    /// pads should behave like stations so dual-dock welds pin against a Static body cleanly.
    /// </summary>
    private void MakeStaticDockPlatform(EntityUid gridUid)
    {
        if (!TryComp<ShuttleComponent>(gridUid, out var shuttle))
            return;

        // ShuttleComponent is not networked — do not Dirty it.
        shuttle.Enabled = false;
        _shuttles.Disable(gridUid);
    }

    /// <summary>
    /// Grid load / FTLDock ordering can leave the shuttle physics body Static even with
    /// <see cref="ShuttleComponent.Enabled"/> true — force Dynamic so undocked flight works.
    /// </summary>
    private void EnsureShuttleFlyable(EntityUid shuttleUid)
    {
        var shuttle = EnsureComp<ShuttleComponent>(shuttleUid);
        // ShuttleComponent is not networked — do not Dirty it.
        shuttle.Enabled = true;
        EnsureComp<FixturesComponent>(shuttleUid);
        var body = EnsureComp<PhysicsComponent>(shuttleUid);

        _shuttles.Enable(shuttleUid, shuttle: shuttle);
        // Always force — Enable can no-op if Resolve fails during map init ordering.
        _physics.SetBodyType(shuttleUid, BodyType.Dynamic, body: body);
        _physics.SetBodyStatus(shuttleUid, body, BodyStatus.InAir);
        _physics.SetFixedRotation(shuttleUid, false, body: body);
    }

    private EntityUid BuildTrainerShuttle(MapId mapId, TutorialShuttleArenaPrototype arena)
    {
        var grid = _map.CreateGridEntity(mapId);
        var gridUid = grid.Owner;
        var floor = (ContentTileDefinition) _tiles["FloorSteel"];
        var tiles = new List<(Vector2i, Tile)>();

        for (var x = 0; x < 11; x++)
        for (var y = 0; y < 7; y++)
            tiles.Add((new Vector2i(x, y), _tile.GetVariantTile(floor, new Random()))); // Starlight edit

        _map.SetTiles(gridUid, grid.Comp, tiles);

        for (var x = 0; x < 11; x++)
        for (var y = 0; y < 7; y++)
        {
            var perimeter = x == 0 || y == 0 || x == 10 || y == 6;
            if (!perimeter)
                continue;
            if (x == 10 && y is 2 or 4)
                continue; // east docks
            if (y == 0 && x is >= 4 and <= 6)
                continue; // thruster bay

            SpawnAnchored("WallSolid", gridUid, new Vector2i(x, y));
        }

        var dockA = SpawnAnchored(arena.DockProto, gridUid, new Vector2i(10, 4));
        var dockB = SpawnAnchored(arena.DockProto, gridUid, new Vector2i(10, 2));
        _transform.SetLocalRotation(dockA, Angle.FromDegrees(90));
        _transform.SetLocalRotation(dockB, Angle.FromDegrees(90));

        SpawnAnchored(arena.ConsoleProto, gridUid, new Vector2i(3, 3));
        SpawnAnchored("ChairPilotSeat", gridUid, new Vector2i(2, 3));

        var thrusterA = SpawnAnchored("Thruster", gridUid, new Vector2i(4, 0));
        var thrusterB = SpawnAnchored("Thruster", gridUid, new Vector2i(6, 0));
        _transform.SetLocalRotation(thrusterA, Angle.FromDegrees(180));
        _transform.SetLocalRotation(thrusterB, Angle.FromDegrees(180));
        SpawnAnchored("Gyroscope", gridUid, new Vector2i(5, 3));
        SpawnAnchored("CargoPallet", gridUid, new Vector2i(7, 3));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(2, 5));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(8, 5));

        var spawn = Spawn("SpawnPointLatejoin", new EntityCoordinates(gridUid, new Vector2(2.5f, 3.5f)));
        EnsureComp<TutorialSpawnPointComponent>(spawn);

        EnsureComp<ShuttleComponent>(gridUid);
        return gridUid;
    }

    /// <summary>
    /// Builds the cargo-bay box station (same hull/furniture as the cargo tutorial home dock).
    /// Used by cargo shuttle arenas and the Space Dragon prey arena.
    /// </summary>
    public EntityUid BuildCargoBayBoxStation(
        MapId mapId,
        EntProtoId dockProto,
        string stationId,
        bool attachTradeStation = true)
    {
        const int w = 15;
        const int h = 11;
        var gridUid = BuildStationHull(mapId, w, h, "FloorSteel", "FloorSteelCheckerDark", westDockYs: new[] { 4, 6 }, dockProto);

        var station = EnsureComp<TutorialDockStationComponent>(gridUid);
        station.StationId = stationId;
        Dirty(gridUid, station);

        if (attachTradeStation)
        {
            var tradeStation = Spawn(TutorialCargoTradeStationProto, MapCoordinates.Nullspace);
            _station.AddGridToStation(tradeStation, gridUid, name: "Tutorial Cargo Bay");
        }

        SpawnAnchored("DefaultStationBeaconCargoBay", gridUid, new Vector2i(7, 9));
        SpawnAnchored("TutorialComputerCargoOrders", gridUid, new Vector2i(4, 8));
        SpawnAnchored("ComputerCargoBounty", gridUid, new Vector2i(6, 8));
        SpawnAnchored("ChairOfficeLight", gridUid, new Vector2i(4, 7));
        SpawnAnchored("OreProcessor", gridUid, new Vector2i(11, 7));
        // Pullable sell crates for early haul practice (must not start on ATS sell pads).
        SpawnBaySellCrate("CrateGenericSteel", gridUid, new Vector2i(9, 3));
        SpawnBaySellCrate("CratePlastic", gridUid, new Vector2i(10, 3));
        SpawnBaySellCrate("CrateGenericSteel", gridUid, new Vector2i(11, 3));
        SpawnAnchored("ConveyorBelt", gridUid, new Vector2i(8, 5));
        SpawnAnchored("ConveyorBelt", gridUid, new Vector2i(9, 5));
        SpawnAnchored("ConveyorBelt", gridUid, new Vector2i(10, 5));
        SpawnAnchored("TableCounterMetal", gridUid, new Vector2i(3, 3));
        SpawnAnchored("Rack", gridUid, new Vector2i(12, 8));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(1, 3));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(1, 6));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(4, 1));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(7, 1));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(10, 1));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(13, 3));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(13, 6));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(3, 9));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(7, 9));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(11, 9));
        SpawnAnchored("HolopadCargoBay", gridUid, new Vector2i(7, 2));

        return gridUid;
    }

    /// <summary>
    /// Rough cargo-bay department: docks, orders console, crates, conveyors, ore pad.
    /// </summary>
    private EntityUid BuildCargoBayStation(MapId mapId, TutorialShuttleArenaPrototype arena)
    {
        return BuildCargoBayBoxStation(mapId, arena.DockProto, arena.HomeStationId, attachTradeStation: true);
    }

    /// <summary>
    /// Gravity + atmos init for a procedural dock station (shared with dragon prey arena).
    /// </summary>
    public void PrepareDockStationGrid(EntityUid gridUid) => PrepareGrid(gridUid);

    /// <summary>
    /// Compact Automated Trade Station stand-in with pallet pads and a facing dock.
    /// </summary>
    private EntityUid BuildAtsStation(MapId mapId, TutorialShuttleArenaPrototype arena)
    {
        const int w = 13;
        const int h = 9;
        var gridUid = BuildStationHull(mapId, w, h, "FloorTechMaint", "FloorDark", westDockYs: new[] { 3, 5 }, arena.DockProto);

        var dockStation = EnsureComp<TutorialDockStationComponent>(gridUid);
        dockStation.StationId = arena.DistantStationId;
        Dirty(gridUid, dockStation);
        EnsureComp<TradeStationComponent>(gridUid);

        SpawnAnchored("ComputerPalletConsole", gridUid, new Vector2i(6, 6));
        SpawnAnchored("TutorialCargoPalletSell", gridUid, new Vector2i(3, 4));
        SpawnAnchored("CargoPalletSell", gridUid, new Vector2i(4, 4));
        SpawnAnchored("CargoPalletBuy", gridUid, new Vector2i(8, 4));
        SpawnAnchored("CargoPalletBuy", gridUid, new Vector2i(9, 4));
        // No preloaded sell crates on pads — Cargo Tech must haul a bay crate to sell.
        SpawnAnchored("HolopadCargoAts", gridUid, new Vector2i(6, 7));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(3, 7));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(9, 7));
        SpawnAnchored("TableCounterMetal", gridUid, new Vector2i(10, 6));

        // Pallet console sale requires an owning station with a bank account.
        var tradeStation = Spawn(TutorialCargoTradeStationProto, MapCoordinates.Nullspace);
        _station.AddGridToStation(tradeStation, gridUid, name: "Tutorial ATS");

        // Bounty crate stays OFF the sell pads (optional prop; not required for Cargo Tech).
        SpawnBountyCrate(tradeStation, gridUid, new Vector2i(6, 3));

        return gridUid;
    }

    private void SpawnBountyCrate(EntityUid tradeStation, EntityUid gridUid, Vector2i tile)
    {
        if (!TryComp<StationCargoBountyDatabaseComponent>(tradeStation, out var bountyDb))
            return;

        _cargo.TryAddBounty(tradeStation, TutorialBounty, bountyDb);

        Content.Shared.Cargo.CargoBountyData? bounty = null;
        foreach (var entry in bountyDb.Bounties)
        {
            if (entry.Bounty != TutorialBounty)
                continue;
            bounty = entry;
            break;
        }

        if (bounty == null)
            return;

        var crate = SpawnSellableCrate("CrateGenericSteel", gridUid, tile);

        // Fill bounty contents (BountyBread needs Bread-tagged food).
        // Ensure Storage is ready — Insert Resolve can fail if MapInit hasn't attached it yet.
        EnsureComp<Content.Shared.Storage.StorageComponent>(crate);
        var bread = Spawn("FoodBreadPlain", Transform(crate).Coordinates);
        _storage.Insert(crate, bread, out _, playSound: false);

        var label = Spawn("PaperCargoBountyManifest", Transform(crate).Coordinates);
        _cargo.SetupBountyLabel(label, tradeStation, bounty.Value);
        _slots.TryInsert(crate, LabelSystem.ContainerName, label, user: null);
    }

    private EntityUid BuildStationHull(
        MapId mapId,
        int width,
        int height,
        string floorId,
        string? altFloorId,
        int[] westDockYs,
        EntProtoId dockProto)
    {
        var grid = _map.CreateGridEntity(mapId);
        var gridUid = grid.Owner;
        var floor = (ContentTileDefinition) _tiles[floorId];
        ContentTileDefinition? alt = null;
        if (!string.IsNullOrEmpty(altFloorId))
            alt = (ContentTileDefinition) _tiles[altFloorId];

        var tiles = new List<(Vector2i, Tile)>(width * height);
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
        {
            var def = alt != null && ((x + y) & 1) == 1 ? alt : floor;
            tiles.Add((new Vector2i(x, y), _tile.GetVariantTile(def, new Random()))); // Starlight edit
        }

        _map.SetTiles(gridUid, grid.Comp, tiles);

        var dockSet = new HashSet<int>(westDockYs);
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
        {
            var perimeter = x == 0 || y == 0 || x == width - 1 || y == height - 1;
            if (!perimeter)
                continue;

            if (x == 0 && dockSet.Contains(y))
                continue;

            var useWindow = y == height - 1 && x > 0 && x < width - 1 && (x % 2 == 1);
            SpawnAnchored(useWindow ? "Window" : "WallSolid", gridUid, new Vector2i(x, y));
        }

        foreach (var y in westDockYs)
        {
            var dock = SpawnAnchored(dockProto, gridUid, new Vector2i(0, y));
            _transform.SetLocalRotation(dock, Angle.FromDegrees(-90));
        }

        return gridUid;
    }

    /// <summary>
    /// Compact dock platform used for nukie (and other non-cargo) shuttle arenas.
    /// </summary>
    private EntityUid BuildDockOnlyStation(
        MapId mapId,
        TutorialShuttleArenaPrototype arena,
        string stationId,
        int width,
        int height)
    {
        var gridUid = BuildStationHull(mapId, width, height, "FloorSteel", "FloorSteelCheckerDark",
            westDockYs: new[] { height / 2 - 1, height / 2 + 1 }, arena.DockProto);

        var station = EnsureComp<TutorialDockStationComponent>(gridUid);
        station.StationId = stationId;
        Dirty(gridUid, station);

        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(3, height - 2));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(width - 4, height - 2));
        SpawnAnchored("ChairOfficeDark", gridUid, new Vector2i(width / 2, height / 2));
        return gridUid;
    }

    private void EnsureTutorialSpawnOnShuttle(EntityUid shuttleUid)
    {
        var existing = EntityQueryEnumerator<TutorialSpawnPointComponent, TransformComponent>();
        while (existing.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == shuttleUid)
                return;
        }

        Vector2 pos;
        var seat = FindProtoOnGrid(shuttleUid, "ChairPilotSeat");
        var helm = FindProtoOnGrid(shuttleUid, "ComputerShuttle")
                   ?? FindProtoOnGrid(shuttleUid, "ComputerShuttleSyndie");
        if (seat != null && TryComp(seat.Value, out TransformComponent? seatXform))
            pos = seatXform.Coordinates.Position;
        else if (helm != null && TryComp(helm.Value, out TransformComponent? helmXform))
            pos = helmXform.Coordinates.Position + new Vector2(1f, 0f);
        else if (TryComp<MapGridComponent>(shuttleUid, out var grid))
            pos = grid.LocalAABB.Center;
        else
            pos = new Vector2(2.5f, 3.5f);

        var spawn = Spawn("SpawnPointLatejoin", new EntityCoordinates(shuttleUid, pos));
        EnsureComp<TutorialSpawnPointComponent>(spawn);
    }

    private void EnsureTutorialSpawnOnCargoBay(EntityUid bayUid)
    {
        var existing = EntityQueryEnumerator<TutorialSpawnPointComponent, TransformComponent>();
        while (existing.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == bayUid)
                return;
        }

        // Near the orders console / practice crates.
        var spawn = Spawn("SpawnPointLatejoin", new EntityCoordinates(bayUid, new Vector2(5.5f, 6.5f)));
        EnsureComp<TutorialSpawnPointComponent>(spawn);
    }

    private void EnsureShuttleBoardMarker(EntityUid shuttleUid)
    {
        var existing = EntityQueryEnumerator<TutorialStepMarkerComponent, TransformComponent>();
        while (existing.MoveNext(out _, out var marker, out var xform))
        {
            if (xform.GridUid == shuttleUid && marker.MarkerId == CargoShuttleBoardMarkerId)
                return;
        }

        Vector2 pos;
        var seat = FindProtoOnGrid(shuttleUid, "ChairPilotSeat");
        var helm = FindProtoOnGrid(shuttleUid, "ComputerShuttle")
                   ?? FindProtoOnGrid(shuttleUid, "ComputerShuttleSyndie");
        if (seat != null && TryComp(seat.Value, out TransformComponent? seatXform))
            pos = seatXform.Coordinates.Position + new Vector2(1f, 0f);
        else if (helm != null && TryComp(helm.Value, out TransformComponent? helmXform))
            pos = helmXform.Coordinates.Position + new Vector2(0f, -1f);
        else if (TryComp<MapGridComponent>(shuttleUid, out var grid))
            pos = grid.LocalAABB.Center;
        else
            pos = new Vector2(3.5f, 3.5f);

        var markerUid = Spawn(TutorialStepMarkerProto, new EntityCoordinates(shuttleUid, pos));
        var markerComp = EnsureComp<TutorialStepMarkerComponent>(markerUid);
        markerComp.MarkerId = CargoShuttleBoardMarkerId;
        Dirty(markerUid, markerComp);
    }

    private EntityUid SpawnBaySellCrate(EntProtoId proto, EntityUid gridUid, Vector2i tile)
    {
        var uid = SpawnSellableCrate(proto, gridUid, tile);
        _tags.AddTag(uid, BayCrateTag);
        return uid;
    }

    private EntityUid? FindProtoOnGrid(EntityUid gridUid, string protoId)
    {
        var query = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;
            if (meta.EntityPrototype?.ID == protoId)
                return uid;
        }

        return null;
    }

    private EntityUid SpawnAnchored(EntProtoId proto, EntityUid gridUid, Vector2i tile)
    {
        var coords = new EntityCoordinates(gridUid, tile.X + 0.5f, tile.Y + 0.5f);
        var uid = Spawn(proto, coords);
        _power.SetNeedsPower(uid, false);
        return uid;
    }

    private EntityUid SpawnSellableCrate(EntProtoId proto, EntityUid gridUid, Vector2i tile)
    {
        var coords = new EntityCoordinates(gridUid, tile.X + 0.5f, tile.Y + 0.5f);
        var uid = Spawn(proto, coords);
        var xform = Transform(uid);
        if (xform.Anchored)
            _transform.Unanchor(uid, xform);
        _power.SetNeedsPower(uid, false);
        return uid;
    }

    private EntityCoordinates ResolveShuttleSpawn(EntityUid shuttleUid)
    {
        return ResolveGridSpawn(shuttleUid, fallback: new Vector2(2.5f, 3.5f));
    }

    private EntityCoordinates ResolveGridSpawn(EntityUid gridUid, Vector2? fallback = null)
    {
        var query = EntityQueryEnumerator<TutorialSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == gridUid)
                return xform.Coordinates;
        }

        return new EntityCoordinates(gridUid, fallback ?? new Vector2(5.5f, 6.5f));
    }
}
