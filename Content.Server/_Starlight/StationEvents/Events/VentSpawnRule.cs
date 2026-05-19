using System.Linq;
using Content.Server._Starlight.StationEvents.Components;
using Content.Server.Antag;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Server.VentCrawl;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Station.Components;
using Content.Shared.VentCrawl;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.StationEvents.Events;

/// <summary>
/// Station event component for spawning this rules antags in vents at station.
/// </summary>
public sealed class VentSpawnRule : StationEventSystem<VentSpawnRuleComponent>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly VentCrawlTubeSystem _ventCrawl = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentSpawnRuleComponent, AntagSelectLocationEvent>(OnSelectLocation);
        SubscribeLocalEvent<VentSpawnRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterSelection);
    }

    private void OnAfterSelection(Entity<VentSpawnRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (ent.Comp.Vent is not { } vent)
            return;

        if (!_ventCrawl.TryInsert(vent.Item2, args.EntityUid))
            Log.Warning($"VentSpawnRule: failed to insert {ToPrettyString(args.EntityUid)} into vent {ToPrettyString(vent.Item2)} — antag spawned outside tube.");
    }

    private void OnSelectLocation(Entity<VentSpawnRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (!TryComp<StationEventComponent>(ent.Owner, out var stationEvent)) return;
        var station = stationEvent.TargetStation;
        if (station is null)
            if (!TryGetRandomStation(out station))
            {
                ForceEndSelf(ent.Owner);
                return;
            }

        var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        var validLocations = new List<(MapCoordinates, EntityUid)>();
        while (locations.MoveNext(out var uid, out _, out var transform))
        {
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station == station)
                validLocations.Add((_transform.GetMapCoordinates(transform), uid));
        }

        if (validLocations.Count == 0)
        {
            ForceEndSelf(ent.Owner);
            return;
        }

        // create the spawner!
        var pair = validLocations[RobustRandom.Next(validLocations.Count)];

        ent.Comp.Vent = pair;
        args.Coordinates.Add(pair.Item1);
        Sawmill.Info($"Picked location {pair.Item1} for {ToPrettyString(ent.Owner):rule}");
    }
}
