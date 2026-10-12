using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Components;
using Content.Shared.PowerCell;
using Content.Shared.Pinpointer;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server.Pinpointer;

public sealed partial class StationMapSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationMapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StationMapUserComponent, EntParentChangedMessage>(OnUserParentChanged);

        SubscribeLocalEvent<NukeopsStationMapComponent, NukeopsTargetStationSelectedEvent>(OnNukeopsStationSelected);

        Subs.BuiEvents<StationMapComponent>(StationMapUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnStationMapOpened);
            subs.Event<BoundUIClosedEvent>(OnStationMapClosed);
        });
    }

    private void OnMapInit(Entity<StationMapComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.InitializeWithStation)
            return;

        // Starlight-start
        // Planning consoles reuse StationMapComponent's target grid and the existing nav-map data.
        // Keep the original handheld nuke map lookup and same-map fallback unchanged; only
        // the tactical console needs a main-station fallback when no nuke rule is active.
        if (HasComp<NukeopsStationMapComponent>(ent))
        {
            if (HasComp<TacticalPlanningConsoleComponent>(ent))
            {
                EnsureNukeopsTarget((ent.Owner, (StationMapComponent?) ent.Comp));
                return;
            }

            foreach (var rule in _gameTicker.GetActiveGameRules())
            {
                if (TryComp<NukeopsRuleComponent>(rule, out var nukeopsRule) && nukeopsRule.TargetStation != null)
                {
                    ent.Comp.TargetGrid = _station.GetLargestGrid((nukeopsRule.TargetStation.Value, null));
                    Dirty(ent);
                    return;
                }
            }
        }

        if (HasComp<TacticalPlanningConsoleComponent>(ent))
        {
            EnsureTacticalPlanningTarget((ent.Owner, (StationMapComponent?) ent.Comp));
            return;
        }
        // Starlight-end

        var station = _station.GetStationInMap(_xform.GetMapId(ent.Owner));
        if (station != null)
        {
            ent.Comp.TargetGrid = _station.GetLargestGrid((station.Value, null));
            Dirty(ent);
        }
    }

    #region Starlight
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
    /// main station on the round's default map rather than the off-station map under the console.
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

        if (target == null)
        {
            // MainGrids identifies grids loaded with a station; the largest such grid on
            // DefaultMap distinguishes the round's station from later/off-map stations.
            // Break equal-size ties by UID so station query order cannot affect the fallback.
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
                        || (size == largestMainGrid && target != null && station.CompareTo(target.Value) >= 0))
                        continue;

                    largestMainGrid = size;
                    target = station;
                }
            }
        }

        var grid = target == null ? null : _station.GetLargestGrid((target.Value, null));
        if (ent.Comp.TargetGrid == grid)
            return;

        ent.Comp.TargetGrid = grid;
        Dirty(ent);
    }

    #endregion

    private void OnStationMapClosed(EntityUid uid, StationMapComponent component, BoundUIClosedEvent args)
    {
        if (!Equals(args.UiKey, StationMapUiKey.Key))
            return;

        RemCompDeferred<StationMapUserComponent>(args.Actor);
    }

    private void OnUserParentChanged(EntityUid uid, StationMapUserComponent component, ref EntParentChangedMessage args)
    {
        _ui.CloseUi(component.Map, StationMapUiKey.Key, uid);
    }

    private void OnStationMapOpened(EntityUid uid, StationMapComponent component, BoundUIOpenedEvent args)
    {
        if (!_cell.TryUseActivatableCharge(uid))
            return;

        var comp = EnsureComp<StationMapUserComponent>(args.Actor);
        comp.Map = uid;
    }

    private void OnNukeopsStationSelected(Entity<NukeopsStationMapComponent> ent, ref NukeopsTargetStationSelectedEvent args)
    {
        if (args.TargetStation == null)
            return;

        if (!TryComp<StationMapComponent>(ent, out var stationMap) || !TryComp<RuleGridsComponent>(args.RuleEntity, out var ruleGrids))
            return;

        if (Transform(ent).MapID != ruleGrids.Map)
            return;

        stationMap.TargetGrid = _station.GetLargestGrid((args.TargetStation.Value, null));
        Dirty(ent);
    }
}
