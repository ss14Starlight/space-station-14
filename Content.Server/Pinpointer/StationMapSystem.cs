using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Components;
using Content.Shared.PowerCell;
using Content.Shared.Pinpointer;
using Content.Shared.Station;
using Robust.Server.GameObjects;

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

        // Planning consoles reuse StationMapComponent's target grid and the existing nav-map data.
        // Choose that grid here before the client receives its first map: syndicate consoles
        // show the operation's station, while ordinary consoles show the grid they are on.
        if (HasComp<NukeopsStationMapComponent>(ent))
        {
            EnsureNukeopsTarget(ent.Owner);
            return;
        }

        if (HasComp<TacticalPlanningConsoleComponent>(ent))
        {
            EnsureTacticalPlanningTarget(ent.Owner);
            return;
        }

        var station = _station.GetStationInMap(_xform.GetMapId(ent.Owner));
        if (station != null)
        {
            ent.Comp.TargetGrid = _station.GetLargestGrid((station.Value, null));
            Dirty(ent);
        }
    }

    /// <summary>
    /// Ordinary planning consoles show their own grid, whether it is a station, ship, or platform.
    /// Recheck on snapshot requests so a console moved to another grid follows its new location.
    /// </summary>
    public void EnsureTacticalPlanningTarget(EntityUid uid)
    {
        if (!HasComp<TacticalPlanningConsoleComponent>(uid) || HasComp<NukeopsStationMapComponent>(uid)
            || !TryComp<StationMapComponent>(uid, out var map))
            return;

        var grid = Transform(uid).GridUid;
        if (map.TargetGrid == grid)
            return;

        map.TargetGrid = grid;
        Dirty(uid, map);
    }

    /// <summary>
    /// When there is no active nuke-ops target (for example in a sandbox test), use the
    /// event-eligible main station rather than the off-station map under the console.
    /// </summary>
    public void EnsureNukeopsTarget(EntityUid uid)
    {
        if (!HasComp<NukeopsStationMapComponent>(uid) || !TryComp<StationMapComponent>(uid, out var map))
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
            foreach (var station in _station.GetStations())
            {
                if (!HasComp<StationEventEligibleComponent>(station))
                    continue;

                target = station;
                break;
            }
        }

        var grid = target == null ? null : _station.GetLargestGrid((target.Value, null));
        if (map.TargetGrid == grid)
            return;

        map.TargetGrid = grid;
        Dirty(uid, map);
    }

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
