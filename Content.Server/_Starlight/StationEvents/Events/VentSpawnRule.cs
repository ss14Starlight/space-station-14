using Content.Server._Starlight.StationEvents.Components;
using Content.Server.Antag;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.VentCrawl;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;

namespace Content.Server._Starlight.StationEvents.Events;

/// <summary>
/// Station event component for spawning this rules antags in vents at station.
/// </summary>
public sealed partial class VentSpawnRule : StationEventSystem<VentSpawnRuleComponent>
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVentCrawlTubeSystem _ventCrawl = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentSpawnRuleComponent, AntagSelectLocationEvent>(OnSelectLocation);
        SubscribeLocalEvent<VentSpawnRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterSelection);
    }

    protected override void Started(EntityUid uid, VentSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        var station = stationEvent.TargetStation;
        if (station is null && !TryGetRandomStation(out station))
        {
            ForceEndSelf(uid);
            return;
        }

        var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        while (locations.MoveNext(out var loc, out _, out var transform))
        {
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station == station)
                comp.ValidLocations.Add((_transform.GetMapCoordinates(transform), loc));
        }

        if (comp.ValidLocations.Count == 0)
            ForceEndSelf(uid);
    }

    private void OnSelectLocation(Entity<VentSpawnRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (ent.Comp.ValidLocations.Count == 0) return;

        var pair = ent.Comp.ValidLocations[RobustRandom.Next(ent.Comp.ValidLocations.Count)];
        ent.Comp.Vent[args.Entity] = pair;
        args.Coordinates.Add(pair.Coords);

        Sawmill.Info($"Picked location {pair.Coords} for {ToPrettyString(ent.Owner):rule}");
    }

    private void OnAfterSelection(Entity<VentSpawnRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!ent.Comp.InsertInVent) return;
        if (!ent.Comp.Vent.TryGetValue(args.EntityUid, out var vent)) return;

        if (!_ventCrawl.TryInsert(vent.Uid, args.EntityUid))
            Log.Warning($"VentSpawnRule: failed to insert {ToPrettyString(args.EntityUid)} into vent {ToPrettyString(vent.Uid)}");
    }
}
