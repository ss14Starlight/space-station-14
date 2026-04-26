using Content.Server.Shuttles.Systems;
using Content.Server.Cargo.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Shipyard;
using Content.Server._Starlight.Shipyard.Components;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.EntitySerialization.Systems;
using Content.Shared.Station.Components;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Starlight.Shipyard.Systems;

public sealed partial class ShipyardSystem : SharedShipyardSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly ShipyardConsoleSystem _shipyardConsole = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public EntityUid? ShipyardMapEntity { get; private set; }
    public MapId? ShipyardMapId { get; private set; }

    private float _shuttleIndex;
    private const float ShuttleSpawnBuffer = 1f;
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        _enabled = _configManager.GetCVar(StarlightCCVars.Shipyard);
        _configManager.OnValueChanged(StarlightCCVars.Shipyard, SetShipyardEnabled);
        _shipyardConsole.InitializeConsole();
        SubscribeLocalEvent<ShipyardConsoleComponent, ComponentInit>(OnShipyardStartup);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnShipyardStartup(EntityUid uid, ShipyardConsoleComponent component, ComponentInit args)
    {
        if (!_enabled)
            return;

        SetupShipyard();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CleanupShipyard();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev) =>
        CleanupShipyard();

    private void SetShipyardEnabled(bool value)
    {
        if (_enabled == value)
            return;

        _enabled = value;

        if (value)
        {
            SetupShipyard();
        }
        else
        {
            CleanupShipyard();
        }
    }

    /// <summary>
    /// Adds a ship to the shipyard, calculates its price, and attempts to ftl-dock it to the given station
    /// </summary>
    /// <param name="stationUid">The ID of the station to dock the shuttle to</param>
    /// <param name="shuttlePath">The path to the shuttle file to load. Must be a grid file!</param>
    public void PurchaseShuttle(EntityUid? stationUid, string shuttlePath, float delay, out ShuttleComponent? vessel)
    {
        vessel = null;

        if (stationUid == null)
            return;

        if (!TryComp(stationUid.Value, out StationDataComponent? stationData))
            return;

        var shuttleUid = AddShuttle(shuttlePath);
        if (shuttleUid == null)
            return;

        if (!TryComp(shuttleUid.Value, out ShuttleComponent? shuttle))
            return;

        var targetGrid = _station.GetLargestGrid((stationUid.Value, stationData));
        if (targetGrid == null)
            return;

        var price = _pricing.AppraiseGrid(shuttleUid.Value, null);

        Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
        {
            if (Deleted(shuttleUid.Value))
                return;

            if (ShipyardMapId == null)
                return;

            if (!targetGrid.HasValue || Deleted(targetGrid.Value))
            {
                Log.Warning($"Target grid vanished before docking shuttle {shuttleUid.Value}");
                return;
            }

            if (!TryComp(shuttleUid.Value, out ShuttleComponent? shuttleComp))
                return;

            _shuttle.TryFTLDock(shuttleUid.Value, shuttleComp, targetGrid.Value);
        });

        vessel = shuttle;

        Log.Info($"Shuttle {shuttlePath} was purchased at {targetGrid} for {price}");
    }

    /// <summary>
    /// Loads a shuttle into the ShipyardMap from a file path
    /// </summary>
    private EntityUid? AddShuttle(string shuttlePath)
    {
        if (ShipyardMapId == null)
            return null;

        if (!_map.TryLoadGrid(ShipyardMapId.Value, new ResPath(shuttlePath), out var grid) || grid == null)
        {
            Log.Error($"Unable to spawn shuttle {shuttlePath}");
            return null;
        }

        var gridUid = grid.Value.Owner;

        // Get width for spacing
        float width = 0f;

        if (TryComp<MapGridComponent>(gridUid, out var gridComp))
        {
            width = gridComp.LocalAABB.Width;
        }

        var offset = _shuttleIndex;

        _shuttleIndex += width + ShuttleSpawnBuffer;

        // Move grid in map space
        _transform.SetWorldPosition(gridUid, new Vector2(offset, 0f));

        return gridUid;
    }

    private void CleanupShipyard()
    {
        if (ShipyardMapEntity == null)
            return;

        if (ShipyardMapId != null)
        {
            var query = EntityQueryEnumerator<MapGridComponent>();

            while (query.MoveNext(out var uid, out _))
            {
                if (Transform(uid).MapID == ShipyardMapId)
                    Del(uid);
            }
        }

        _shuttleIndex = 0f;

        if (Exists(ShipyardMapEntity.Value))
            Del(ShipyardMapEntity.Value);

        ShipyardMapEntity = null;
        ShipyardMapId = null;
    }

    private void SetupShipyard()
    {
        if (ShipyardMapEntity != null && Exists(ShipyardMapEntity.Value))
            return;

        ShipyardMapEntity = _mapSystem.CreateMap();

        if (!TryComp<MapComponent>(ShipyardMapEntity.Value, out var mapComp))
        {
            Log.Error($"Created shipyard map {ShipyardMapEntity} has no MapComponent; aborting setup.");
            // Clean up the map we just created
            if (Exists(ShipyardMapEntity.Value))
                Del(ShipyardMapEntity.Value);

            ShipyardMapEntity = null;
            return;
        }
    }
}
