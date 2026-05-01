using Content.Server._Starlight.StationEvents.Components;
using Content.Server.Antag;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map;

namespace Content.Server._Starlight.StationEvents.Events;

/// <summary>
/// Station event component for spawning this rules antags in vents at station.
/// </summary>
public sealed class VentSpawnRule : StationEventSystem<VentSpawnRuleComponent>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentSpawnRuleComponent, AntagSelectLocationEvent>(OnSelectLocation);
    }

    protected override void Added(EntityUid uid, VentSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        //Starlight begin | Prefer target station if there is one, if SOMEHOW that odesn't exist, fallback to existing trygetrandomstation call
        EntityUid? station = null;
        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        station = stationEvent.TargetStation;
        if (station is null)
            if (!TryGetRandomStation(out station))
            {
                ForceEndSelf(uid, gameRule);
                return;
            }
        //Starlight end

        var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        var validLocations = new List<EntityCoordinates>();
        while (locations.MoveNext(out _, out _, out var transform))
        {
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station == station)
                validLocations.Add(transform.Coordinates);
        }

        // create the spawner!
        var position = validLocations[Random.Shared.Next(validLocations.Count)];
        comp.Coords = _transform.ToMapCoordinates(position);
        Sawmill.Info($"Picked location {comp.Coords} for {ToPrettyString(uid):rule}");
    }

    private void OnSelectLocation(Entity<VentSpawnRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (ent.Comp.Coords is { } coords)
            args.Coordinates.Add(coords);
    }
}
