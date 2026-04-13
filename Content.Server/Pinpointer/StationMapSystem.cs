using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.PowerCell;
using Content.Shared.Pinpointer;
using Content.Shared.Station;

namespace Content.Server.Pinpointer;

public sealed partial class StationMapSystem : EntitySystem
{
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationMapComponent, MapInitEvent>(OnMapInit);
        // Starlight: Parent changes no longer close an open Station Map.

        SubscribeLocalEvent<NukeopsStationMapComponent, ChooseStationMapEvent>(OnNukeOpsStationMap);
        SubscribeLocalEvent<NukeopsTargetStationSelectedEvent>(OnNukeopsStationSelected);

        // Starlight - start
        Subs.BuiEvents<StationMapComponent>(StationMapUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnStationMapOpened);
        });
        // Starlight - end
    }

    private void OnMapInit(Entity<StationMapComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.InitializeWithStation)
            return;

        var ev = new ChooseStationMapEvent();
        RaiseLocalEvent(ent, ref ev);
        if (ev.Handled)
        {
            ent.Comp.TargetGrid = ev.TargetGrid;
            Dirty(ent);
            return;
        }

        var station = _station.GetStationInMap(_xform.GetMapId(ent.Owner));
        if (station != null)
        {
            // Starlight - start
            ent.Comp.TargetGrid = GetMainStationGrid(station.Value);
            // Starlight - end
            Dirty(ent);
        }
    }

    private void OnStationMapOpened(EntityUid uid, StationMapComponent component, BoundUIOpenedEvent args)
    {
        // Starlight - start
        if (!_cell.TryUseActivatableCharge(uid))
            return;
        // Starlight - end
    }

    private void OnNukeOpsStationMap(Entity<NukeopsStationMapComponent> entity, ref ChooseStationMapEvent args)
    {
        // If we have this component, we don't want a fallback map!
        args.Handle();

        foreach (var rule in _gameTicker.GetActiveGameRules<NukeopsRuleComponent>())
        {
            if (rule.Comp.TargetStation == null)
                continue;

            args.TargetGrid = GetMainStationGrid(rule.Comp.TargetStation.Value); // Starlight
            return;
        }
    }

    private void OnNukeopsStationSelected(ref NukeopsTargetStationSelectedEvent args)
    {
        if (args.TargetStation == null || !TryComp<RuleGridsComponent>(args.RuleEntity, out var ruleGrids))
            return;

        var mapquery = EntityQueryEnumerator<NukeopsStationMapComponent, StationMapComponent>();
        while (mapquery.MoveNext(out var uid, out _, out var map))
        {
            if (Transform(uid).MapID != ruleGrids.Map)
                continue;

            map.TargetGrid = GetMainStationGrid(args.TargetStation.Value); // Starlight
            Dirty(uid, map);
        }
    }
}

/// <summary>
/// Selects an alternative target for our station map!
/// If handled, this will not get the map of the current station.
/// </summary>
[ByRefEvent]
public record struct ChooseStationMapEvent
{
    public EntityUid? TargetGrid;
    public bool Handled { get; private set; }

    public void Handle()
    {
        Handled = true;
    }
}
