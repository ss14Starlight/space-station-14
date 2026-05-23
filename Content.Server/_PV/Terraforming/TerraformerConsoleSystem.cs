using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared._PV.Terraforming;
using Content.Shared.Atmos;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._PV.Terraforming;

public sealed class TerraformerConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerraformerConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<TerraformerConsoleComponent, TerraformerConsoleRefreshMessage>(OnRefresh);
    }

    private void OnUiOpened(EntityUid uid, TerraformerConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateUserInterface(uid);
    }

    private void OnRefresh(EntityUid uid, TerraformerConsoleComponent component, TerraformerConsoleRefreshMessage args)
    {
        UpdateUserInterface(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TerraformerConsoleComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (!_ui.IsUiOpen(uid, TerraformerConsoleUiKey.Key))
                continue;

            UpdateUserInterface(uid);
        }
    }

    private void UpdateUserInterface(EntityUid consoleUid)
    {
        if (!TryComp<TransformComponent>(consoleUid, out var consoleXform))
            return;

        var entries = BuildTerraformerEntries(consoleXform);
        var totalTiles = 0;

        foreach (var entry in entries)
        {
            totalTiles += entry.TilesTerraformed;
        }

        var atmosphere = BuildAtmosphereSummary(consoleXform, entries);

        NetEntity? gridEntity = null;

        if (consoleXform.GridUid != null)
            gridEntity = GetNetEntity(consoleXform.GridUid.Value);

        var state = new TerraformerConsoleBoundInterfaceState(
            gridEntity,
            entries.Count,
            totalTiles,
            entries,
            atmosphere);

        _ui.SetUiState(consoleUid, TerraformerConsoleUiKey.Key, state);
    }

    private List<TerraformerConsoleEntry> BuildTerraformerEntries(TransformComponent consoleXform)
    {
        var entries = new List<TerraformerConsoleEntry>();
        var query = EntityQueryEnumerator<TerraformerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var terraformer, out var xform))
        {
            if (!IsSameNetworkArea(consoleXform, xform))
                continue;

            var powered = IsPowered(uid);
            var status = GetStatus(terraformer, powered);
            var pos = GetGridPosition(xform);

            entries.Add(new TerraformerConsoleEntry(
                GetNetEntity(uid),
                GetNetCoordinates(xform.Coordinates),
                MetaData(uid).EntityName,
                status,
                terraformer.Fuel,
                terraformer.MaxFuel,
                terraformer.Radius,
                GetBarrierRadius(terraformer),
                terraformer.TilesTerraformed,
                pos.X,
                pos.Y));
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    private TerraformerConsoleStatus GetStatus(TerraformerComponent terraformer, bool powered)
    {
        if (!terraformer.Active || !powered)
            return TerraformerConsoleStatus.Inactive;

        if (terraformer.Fuel <= 0)
            return TerraformerConsoleStatus.Empty;

        return TerraformerConsoleStatus.Working;
    }

    private TerraformerAtmosphereSummary BuildAtmosphereSummary(
        TransformComponent consoleXform,
        List<TerraformerConsoleEntry> entries)
    {
        if (consoleXform.GridUid == null)
            return EmptyAtmosphereSummary();

        var gridUid = consoleXform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return EmptyAtmosphereSummary();

        var sampledTiles = new HashSet<Vector2i>();

        foreach (var entry in entries)
        {
            var radius = MathF.Max(entry.Radius, 0f);
            var radiusInt = (int) MathF.Ceiling(radius);
            var radiusSquared = radius * radius;
            var center = new Vector2i(entry.GridX, entry.GridY);

            for (var x = -radiusInt; x <= radiusInt; x++)
            {
                for (var y = -radiusInt; y <= radiusInt; y++)
                {
                    if (x * x + y * y > radiusSquared)
                        continue;

                    var tile = center + new Vector2i(x, y);

                    if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef))
                        continue;

                    if (tileRef.Tile.IsEmpty)
                        continue;

                    sampledTiles.Add(tile);
                }
            }
        }

        if (sampledTiles.Count == 0)
            return EmptyAtmosphereSummary();

        var mapUid = consoleXform.MapUid;
        var tileCount = 0;
        var pressureTotal = 0f;
        var temperatureTotal = 0f;

        var oxygen = 0f;
        var nitrogen = 0f;
        var carbonDioxide = 0f;
        var plasma = 0f;
        var tritium = 0f;
        var nitrousOxide = 0f;
        var totalMoles = 0f;

        foreach (var tile in sampledTiles)
        {
            var mixture = _atmosphere.GetTileMixture(gridUid, mapUid, tile, true);

            if (mixture == null)
                continue;

            tileCount++;
            pressureTotal += mixture.Pressure;
            temperatureTotal += mixture.Temperature;

            oxygen += mixture.GetMoles(Gas.Oxygen);
            nitrogen += mixture.GetMoles(Gas.Nitrogen);
            carbonDioxide += mixture.GetMoles(Gas.CarbonDioxide);
            plasma += mixture.GetMoles(Gas.Plasma);
            tritium += mixture.GetMoles(Gas.Tritium);
            nitrousOxide += mixture.GetMoles(Gas.NitrousOxide);

            for (var gas = 0; gas < Atmospherics.TotalNumberOfGases; gas++)
            {
                totalMoles += mixture.GetMoles((Gas) gas);
            }
        }

        if (tileCount == 0 || totalMoles <= Atmospherics.GasMinMoles)
            return EmptyAtmosphereSummary(tileCount);

        var trackedMoles = oxygen + nitrogen + carbonDioxide + plasma + tritium + nitrousOxide;
        var other = MathF.Max(totalMoles - trackedMoles, 0f);

        return new TerraformerAtmosphereSummary(
            tileCount,
            pressureTotal / tileCount,
            temperatureTotal / tileCount,
            Percent(oxygen, totalMoles),
            Percent(nitrogen, totalMoles),
            Percent(carbonDioxide, totalMoles),
            Percent(plasma, totalMoles),
            Percent(tritium, totalMoles),
            Percent(nitrousOxide, totalMoles),
            Percent(other, totalMoles));
    }

    private TerraformerAtmosphereSummary EmptyAtmosphereSummary(int tileCount = 0)
    {
        return new TerraformerAtmosphereSummary(
            tileCount,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f);
    }

    private float Percent(float value, float total)
    {
        if (total <= 0f)
            return 0f;

        return value / total * 100f;
    }

    private bool IsSameNetworkArea(TransformComponent consoleXform, TransformComponent terraformerXform)
    {
        if (consoleXform.MapID != terraformerXform.MapID)
            return false;

        if (consoleXform.GridUid != null && terraformerXform.GridUid != null)
            return consoleXform.GridUid == terraformerXform.GridUid;

        return true;
    }

    private Vector2i GetGridPosition(TransformComponent xform)
    {
        if (xform.GridUid == null)
            return Vector2i.Zero;

        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return Vector2i.Zero;

        var tile = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
        return tile.GridIndices;
    }

    private bool IsPowered(EntityUid uid)
    {
        if (!TryComp<ApcPowerReceiverComponent>(uid, out var power))
            return true;

        return power.Powered;
    }

    private float GetBarrierRadius(TerraformerComponent terraformer)
    {
        return terraformer.BarrierRadius > 0f
            ? terraformer.BarrierRadius
            : terraformer.Radius;
    }
}