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

        SubscribeLocalEvent<NukeopsStationMapComponent, NukeopsTargetStationSelectedEvent>(OnNukeopsStationSelected);

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

        // If we ever find a need to make more exceptions like this, just turn this into an event.
        if (HasComp<NukeopsStationMapComponent>(ent))
        {
            foreach (var rule in _gameTicker.GetActiveGameRules())
            {
                if (TryComp<NukeopsRuleComponent>(rule, out var nukeopsRule) && nukeopsRule.TargetStation != null)
                {
                    ent.Comp.TargetGrid = GetMainStationGrid(nukeopsRule.TargetStation.Value); // Starlight
                    Dirty(ent);
                    return;
                }
            }
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

    private void OnNukeopsStationSelected(Entity<NukeopsStationMapComponent> ent, ref NukeopsTargetStationSelectedEvent args)
    {
        if (args.TargetStation == null)
            return;

        if (!TryComp<StationMapComponent>(ent, out var stationMap) || !TryComp<RuleGridsComponent>(args.RuleEntity, out var ruleGrids))
            return;

        if (Transform(ent).MapID != ruleGrids.Map)
            return;

        stationMap.TargetGrid = GetMainStationGrid(args.TargetStation.Value); // Starlight
        Dirty(ent);
    }
}
