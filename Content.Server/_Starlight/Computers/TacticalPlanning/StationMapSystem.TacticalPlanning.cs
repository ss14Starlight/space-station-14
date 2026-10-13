using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Components;
using Content.Shared._Starlight.Computers.TacticalPlanning;
using Content.Shared.Pinpointer;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Pinpointer;

public sealed partial class StationMapSystem
{
    /// <summary>
    /// Ordinary planning consoles show their own grid, whether it is a station, ship, or platform.
    /// Recheck on snapshot requests so a console moved to another grid follows its new location.
    /// </summary>
    public void EnsureTacticalPlanningTarget(Entity<StationMapComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !HasComp<TacticalPlanningConsoleComponent>(ent)
            || HasComp<NukeopsStationMapComponent>(ent))
            return;

        var grid = Transform(ent).GridUid;
        if (ent.Comp.TargetGrid == grid)
            return;

        ent.Comp.TargetGrid = grid;
        Dirty(ent);
    }

    /// <summary>
    /// When there is no active nuke-ops target (for example in a sandbox test), use the
    /// main station grid on the round's default map rather than the off-station map under the console.
    /// </summary>
    public void EnsureNukeopsTarget(Entity<StationMapComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !HasComp<TacticalPlanningConsoleComponent>(ent)
            || !HasComp<NukeopsStationMapComponent>(ent))
            return;

        EntityUid? target = null;
        foreach (var rule in _gameTicker.GetActiveGameRules())
        {
            if (TryComp<NukeopsRuleComponent>(rule, out var nukeopsRule) && nukeopsRule.TargetStation != null)
            {
                target = nukeopsRule.TargetStation;
                break;
            }
        }

        EntityUid? fallbackGrid = null;
        if (target == null)
        {
            // MainGrids identifies grids loaded with a station; the largest such grid on
            // DefaultMap distinguishes the round's station from later/off-map stations.
            // Break equal-size ties by grid UID so station query order cannot affect the fallback.
            var largestMainGrid = -1f;
            foreach (var station in _station.GetStations())
            {
                if (!HasComp<StationEventEligibleComponent>(station)
                    || !TryComp<StationDataComponent>(station, out var data))
                    continue;

                foreach (var mainGrid in data.MainGrids)
                {
                    if (Transform(mainGrid).MapID != _gameTicker.DefaultMap
                        || !TryComp<MapGridComponent>(mainGrid, out var mainGridComp))
                        continue;

                    var size = mainGridComp.LocalAABB.Size.LengthSquared();
                    if (size < largestMainGrid
                        || (size == largestMainGrid && fallbackGrid != null && mainGrid.CompareTo(fallbackGrid.Value) >= 0))
                        continue;

                    largestMainGrid = size;
                    fallbackGrid = mainGrid;
                }
            }
        }

        var grid = target == null ? fallbackGrid : _station.GetLargestGrid((target.Value, null));
        if (ent.Comp.TargetGrid == grid)
            return;

        ent.Comp.TargetGrid = grid;
        Dirty(ent);
    }
}
