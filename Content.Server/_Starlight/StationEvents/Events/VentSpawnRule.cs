using Content.Server._Starlight.StationEvents.Components;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Content.Shared._Starlight.VentCrawl.EntitySystems;
using Content.Shared._Starlight.VentCrawl.Components;
using Robust.Shared.Map;

namespace Content.Server._Starlight.StationEvents.Events;

/// <summary>
/// Station event component for spawning this rules antags in vents at station.
/// </summary>
public sealed partial class VentSpawnRule : StationEventSystem<VentSpawnRuleComponent>
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVentCrawlSystem _ventCrawl = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentSpawnRuleComponent, AntagSelectLocationEvent>(OnSelectLocation);
        SubscribeLocalEvent<VentSpawnRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterSelection);
    }

    protected override void Added(EntityUid uid, VentSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        comp.TargetStation = ResolveTargetStation(uid);
        if (comp.TargetStation != null)
            PopulateValidLocations(comp, comp.TargetStation.Value);
    }

    protected override void Started(EntityUid uid, VentSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
        => base.Started(uid, comp, gameRule, args);

    private EntityUid? ResolveTargetStation(EntityUid uid)
    {
        if (CompOrNull<StationEventComponent>(uid)?.TargetStation is { } station)
            return station;

        if (TryComp<RuleGridsComponent>(uid, out var grids))
        {
            if (grids.Map != null && StationSystem.GetStationInMap(grids.Map.Value) is { } mapStation)
                return mapStation;

            foreach (var grid in grids.MapGrids)
            {
                if (TryComp<StationMemberComponent>(grid, out var member))
                    return member.Station;
            }
        }

        if (StationSystem.GetStationInMap(GameTicker.DefaultMap) is { } defaultStation)
            return defaultStation;

        var stations = StationSystem.GetStations();
        return stations.Count > 0 ? stations[0] : null;
    }

    private void PopulateValidLocations(VentSpawnRuleComponent comp, EntityUid station)
    {
        comp.ValidLocations.Clear();

        var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        while (locations.MoveNext(out var loc, out _, out var transform))
        {
            if (!transform.Anchored || !HasComp<VentCrawlEntryComponent>(loc) ||
                !TryComp<VentCrawlTubeComponent>(loc, out var tube) ||
                !tube.Connected)
            {
                continue;
            }

            var member = CompOrNull<StationMemberComponent>(transform.GridUid);
            if (member == null)
                continue;

            if (member.Station != station)
                continue;

            comp.ValidLocations.Add((_transform.GetMapCoordinates(transform), loc));
        }
    }

    private void OnSelectLocation(Entity<VentSpawnRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (ent.Comp.ValidLocations.Count == 0)
        {
            // If selection happens before vents were fully available, refresh locations now.
            ent.Comp.TargetStation ??= ResolveTargetStation(ent.Owner);
            if (ent.Comp.TargetStation != null)
                PopulateValidLocations(ent.Comp, ent.Comp.TargetStation.Value);
        }

        if (ent.Comp.ValidLocations.Count == 0) return;

        var pair = ent.Comp.ValidLocations[RobustRandom.Next(ent.Comp.ValidLocations.Count)];
        ent.Comp.Vent[args.Antag.ID] = pair;
        args.Coordinates.Add(pair.Coords);

        Sawmill.Info($"Picked location {pair.Coords} for {ToPrettyString(ent.Owner):rule}");
    }

    private void OnAfterSelection(Entity<VentSpawnRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!ent.Comp.InsertInVent) return;
        if (!ent.Comp.Vent.TryGetValue(args.Def.ID, out var vent))
            return;

        if (TryInsertInVent(args.EntityUid, vent))
            return;

        ent.Comp.ValidLocations.Remove(vent);

        while (ent.Comp.ValidLocations.Count > 0)
        {
            vent = ent.Comp.ValidLocations[RobustRandom.Next(ent.Comp.ValidLocations.Count)];
            ent.Comp.Vent[args.Def.ID] = vent;
            _transform.SetMapCoordinates(args.EntityUid, vent.Coords);

            if (TryInsertInVent(args.EntityUid, vent))
                return;

            ent.Comp.ValidLocations.Remove(vent);
        }

        Log.Warning($"VentSpawnRule: failed to insert {ToPrettyString(args.EntityUid)}. Last tried vent: {ToPrettyString(vent.Uid)}; rule: {ToPrettyString(ent.Owner)}");
    }

    private bool TryInsertInVent(EntityUid uid, (MapCoordinates Coords, EntityUid Uid) vent)
    {
        if (!HasComp<VentCrawlEntryComponent>(vent.Uid) ||
            !TryComp<VentCrawlTubeComponent>(vent.Uid, out var tube) ||
            !tube.Connected)
        {
            return false;
        }

        return _ventCrawl.TryInsert(vent.Uid, uid);
    }
}
