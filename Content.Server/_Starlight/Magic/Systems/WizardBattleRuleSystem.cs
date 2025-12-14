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
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    protected override void Added(EntityUid uid, WizardBattleRuleComponent comp, GameRuleComponent rule, GameRuleAddedEvent args)
    {
        // Find the station's map
        var stationQuery = EntityQueryEnumerator<StationDataComponent>();
        if (!stationQuery.MoveNext(out var stationUid, out var stationData))
        {
            Log.Error("No station found for Wizard Battle!");
            ForceEndSelf(uid, rule);
            return;
        }

        // Get the map ID from the station's first grid
        if (stationData.Grids.Count == 0)
        {
            Log.Error("Station has no grids!");
            ForceEndSelf(uid, rule);
            return;
        }

        var stationGrid = stationData.Grids.First();
        var mapId = Transform(stationGrid).MapID;

        // Get the position of the largest station grid to place shuttles near it
        var largestGrid = _station.GetLargestGrid((stationUid, stationData));
        if (largestGrid == null)
        {
            Log.Error("Station has no largest grid!");
            ForceEndSelf(uid, rule);
            return;
        }

        var stationPos = _transform.GetWorldPosition(largestGrid.Value);

        // Load red faction shuttle on the station's map
        var redOpts = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_mapLoader.TryLoadGrid(mapId, comp.ShuttlePathBlue, out var blueGrid, blueOpts))
        {
            Log.Error($"Failed to load blue wizard shuttle from {comp.ShuttlePathBlue}!");
            ForceEndSelf(uid, rule);
            return;
        }

        if (!_mapLoader.TryLoadGrid(mapId, comp.ShuttlePathRed, out var redGrid, redOpts))
        {
            Log.Error($"Failed to load red wizard shuttle from {comp.ShuttlePathRed}!");
            ForceEndSelf(uid, rule);
            return;
        }

        // Position the red shuttle near the station
        var redPos = stationPos + comp.RedOffset;
        _transform.SetWorldPosition(redGrid.Value, redPos);


        // Position the blue shuttle near the station
        var bluePos = stationPos + comp.BlueOffset;
        _transform.SetWorldPosition(blueGrid.Value, bluePos);

        // Notify RuleGridsComponent about the loaded grids
        var grids = new List<EntityUid> { redGrid.Value.Owner, blueGrid.Value.Owner };
        var ev = new RuleLoadedGridsEvent(mapId, grids);
        RaiseLocalEvent(uid, ref ev);

        base.Added(uid, comp, rule, args);
    }
}