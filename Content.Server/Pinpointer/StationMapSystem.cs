using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared._Starlight.Computers.TacticalPlanning;
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
