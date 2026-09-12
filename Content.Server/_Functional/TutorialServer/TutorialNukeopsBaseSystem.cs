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
/// Builds a floating Syndicate outpost fragment: spawn lounge + chem lab, exterior sealed.
/// </summary>
public sealed partial class TutorialNukeopsBaseSystem : EntitySystem
{
    private const int Width = 13;
    private const int Height = 21;
    private const int ChemMaxY = 9; // chem interior y = 1..8; divider at y=9
    private const int LoungeMinY = 10; // lounge interior y = 10..19

    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    // [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    // [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TileSystem _tile = default!;

    public bool TryBuildOutpost(
        out EntityUid mapUid,
        out EntityUid gridUid,
        out EntityCoordinates spawnCoords)
    {
        mapUid = _map.CreateMap(out var mapId);
        gridUid = BuildGrid(mapId);
        // Marker for tests / tooling; force-power + freeze applied via SimplifiedEnvironment.
        EnsureComp<TutorialForcePowerGridComponent>(gridUid);
        PrepareAtmosphere(gridUid);

        spawnCoords = new EntityCoordinates(gridUid, new Vector2(6.5f, 15.5f));
        var spawn = Spawn("SpawnPointLatejoin", spawnCoords);
        EnsureComp<TutorialSpawnPointComponent>(spawn);

        Log.Info($"TUTORIAL_E2E: nukeops_outpost_ready map={mapUid} grid={gridUid}");
        return true;
    }

    private EntityUid BuildGrid(MapId mapId)
    {
        var grid = _map.CreateGridEntity(mapId);
        var gridUid = grid.Owner;
        var wood = (ContentTileDefinition) _tiles["FloorWood"];
        var steel = (ContentTileDefinition) _tiles["FloorWhite"];
        var tiles = new List<(Vector2i, Tile)>(Width * Height);

        for (var x = 0; x < Width; x++)
        for (var y = 0; y < Height; y++)
        {
            var def = y <= ChemMaxY ? steel : wood;
            tiles.Add((new Vector2i(x, y), _tile.GetVariantTile(def, new Random()))); // Starlight edit
        }

        _map.SetTiles(gridUid, grid.Comp, tiles);

        // Perimeter + divider walls (plastitanium), leave connecting door gap.
        for (var x = 0; x < Width; x++)
        for (var y = 0; y < Height; y++)
        {
            var perimeter = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
            var divider = y == ChemMaxY && x > 0 && x < Width - 1;
            if (!perimeter && !divider)
                continue;

            // Connecting door between lounge and chem.
            if (divider && x == 6)
            {
                SpawnAnchored("AirlockSyndicateGlass", gridUid, new Vector2i(x, y));
                continue;
            }

            SpawnAnchored("WallPlastitanium", gridUid, new Vector2i(x, y));
        }

        PlaceLounge(gridUid);
        PlaceChem(gridUid);
        return gridUid;
    }

    private void PlaceLounge(EntityUid gridUid)
    {
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(3, 19));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(9, 19));
        SpawnAnchored("TableWood", gridUid, new Vector2i(4, 17));
        SpawnAnchored("TableWood", gridUid, new Vector2i(6, 17));
        SpawnAnchored("ChairWood", gridUid, new Vector2i(4, 16));
        SpawnAnchored("ChairWood", gridUid, new Vector2i(6, 16));
        SpawnAnchored("ChairWood", gridUid, new Vector2i(5, 16));
        SpawnAnchored("Fireplace", gridUid, new Vector2i(2, 18));
        SpawnAnchored("BoozeDispenser", gridUid, new Vector2i(9, 17));
        SpawnAnchored("VendingMachineBoozeSyndicate", gridUid, new Vector2i(10, 15));
        SpawnAnchored("VendingMachineSnack", gridUid, new Vector2i(10, 13));
        SpawnAnchored("VendingMachineCola", gridUid, new Vector2i(10, 11));
        SpawnAnchored("ComputerNukieDelivery", gridUid, new Vector2i(2, 14));
        SpawnAnchored("BannerSyndicate", gridUid, new Vector2i(2, 12));
        SpawnAnchored("Rack", gridUid, new Vector2i(3, 11));
        Spawn("HandheldStationMapNukeops", new EntityCoordinates(gridUid, 4.5f, 17.3f));
        Spawn("PlushieNuke", new EntityCoordinates(gridUid, 6.5f, 17.3f));
    }

    private void PlaceChem(EntityUid gridUid)
    {
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(3, 8));
        SpawnAnchored("AlwaysPoweredWallLight", gridUid, new Vector2i(9, 8));

        // Tutorial-powered chem machines (authentic layout, reliable on void grids).
        SpawnAnchored("TutorialChemDispenser", gridUid, new Vector2i(8, 6));
        SpawnAnchored("TutorialChemMaster", gridUid, new Vector2i(8, 4));
        SpawnAnchored("ChemistryHotplate", gridUid, new Vector2i(6, 6));
        SpawnAnchored("TutorialKitchenReagentGrinder", gridUid, new Vector2i(4, 4));
        SpawnAnchored("VendingMachineChemicalsSyndicate", gridUid, new Vector2i(4, 6));
        SpawnAnchored("MachineCentrifuge", gridUid, new Vector2i(2, 5));
        SpawnAnchored("MachineElectrolysisUnit", gridUid, new Vector2i(2, 3));
        SpawnAnchored("TableGlass", gridUid, new Vector2i(6, 3));
        SpawnAnchored("TableGlass", gridUid, new Vector2i(5, 3));

        Spawn("Beaker", new EntityCoordinates(gridUid, 6.5f, 3.3f));
        Spawn("Beaker", new EntityCoordinates(gridUid, 5.7f, 3.3f));
        Spawn("LargeBeaker", new EntityCoordinates(gridUid, 5.3f, 3.3f));
        Spawn("BoxBeaker", new EntityCoordinates(gridUid, 6.2f, 2.5f));
        Spawn("BoxPillCanister", new EntityCoordinates(gridUid, 5.2f, 2.5f));
        Spawn("ClothingEyesGlassesChemical", new EntityCoordinates(gridUid, 7.2f, 3.3f));
        Spawn("HandLabeler", new EntityCoordinates(gridUid, 7.5f, 2.5f));
    }

    private void PrepareAtmosphere(EntityUid gridUid)
    {
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

    private EntityUid SpawnAnchored(EntProtoId proto, EntityUid gridUid, Vector2i tile)
    {
        var coords = new EntityCoordinates(gridUid, tile.X + 0.5f, tile.Y + 0.5f);
        var uid = Spawn(proto, coords);
        _power.SetNeedsPower(uid, false);
        return uid;
    }
}
