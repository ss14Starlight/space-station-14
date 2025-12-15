using Content.Server._Starlight.Magic.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Maps;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Content.Shared.Station;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Robust.Shared.Maths;
using System.Linq;
using System.Numerics;

namespace Content.Server._Starlight.Magic.Systems;

/// <summary>
/// Handles loading the two wizard shuttles for the Wizard Battle game rule.
/// </summary>
public sealed class WizardBattleRuleSystem : GameRuleSystem<WizardBattleRuleComponent>
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    protected override void Added(EntityUid uid, WizardBattleRuleComponent comp, GameRuleComponent rule, GameRuleAddedEvent args)
    {
        // Create a temporary map to load the shuttles
        var tempMap = _map.CreateMap(out var tempMapId);

        // Load red faction shuttle on the temporary map
        var redOpts = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_mapLoader.TryLoadGrid(tempMapId, comp.ShuttlePathRed, out var redGrid, redOpts))
        {
            Log.Error($"Failed to load red wizard shuttle from {comp.ShuttlePathRed}!");
            ForceEndSelf(uid, rule);
            return;
        }

        // Load blue faction shuttle on the temporary map
        var blueOpts = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_mapLoader.TryLoadGrid(tempMapId, comp.ShuttlePathBlue, out var blueGrid, blueOpts))
        {
            Log.Error($"Failed to load blue wizard shuttle from {comp.ShuttlePathBlue}!");
            ForceEndSelf(uid, rule);
            return;
        }

        // Store the loaded grids in the component
        comp.RedShuttle = redGrid.Value.Owner;
        comp.BlueShuttle = blueGrid.Value.Owner;
        comp.TempMapId = tempMapId;

        base.Added(uid, comp, rule, args);
    }

    protected override void Started(EntityUid uid, WizardBattleRuleComponent comp, GameRuleComponent rule, GameRuleStartedEvent args)
    {
        // Find the station
        var stationQuery = EntityQueryEnumerator<StationDataComponent>();
        if (!stationQuery.MoveNext(out var stationUid, out var stationData))
        {
            Log.Error("No station found for Wizard Battle!");
            return;
        }

        // Get the map ID from the station's largest grid
        var largestGrid = _station.GetLargestGrid((stationUid, stationData));
        if (largestGrid == null)
        {
            Log.Error("Station has no largest grid!");
            return;
        }

        var mapId = Transform(largestGrid.Value).MapID;
        var stationPos = _transform.GetWorldPosition(largestGrid.Value);

        // Move the shuttles to the station's map
        if (comp.RedShuttle.HasValue)
        {
            _transform.SetParent(comp.RedShuttle.Value, _mapManager.GetMapEntityId(mapId));
            var redPos = stationPos + comp.RedOffset;
            _transform.SetWorldPosition(comp.RedShuttle.Value, redPos);
        }

        if (comp.BlueShuttle.HasValue)
        {
            _transform.SetParent(comp.BlueShuttle.Value, _mapManager.GetMapEntityId(mapId));
            var bluePos = stationPos + comp.BlueOffset;
            _transform.SetWorldPosition(comp.BlueShuttle.Value, bluePos);
        }

        // Delete the temporary map
        if (comp.TempMapId.HasValue)
        {
            _map.DeleteMap(comp.TempMapId.Value);
        }

        // Notify RuleGridsComponent about the loaded grids
        var grids = new List<EntityUid>();
        if (comp.RedShuttle.HasValue) grids.Add(comp.RedShuttle.Value);
        if (comp.BlueShuttle.HasValue) grids.Add(comp.BlueShuttle.Value);
        var ev = new RuleLoadedGridsEvent(mapId, grids);
        RaiseLocalEvent(uid, ref ev);

        base.Started(uid, comp, rule, args);
    }
}