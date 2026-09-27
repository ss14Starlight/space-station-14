// ReSharper disable CheckNamespace

using System.Linq;
using Content.Server._Starlight.Station;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Station.Systems;

public sealed partial class StationSystem
{
    public bool IsEntityOnStation(EntityUid uid, EntityUid? station, StationDataComponent? component = null)
    {
        if (station is null) return false;

        if (!Resolve(station.Value, ref component))
            return false;

        var grid = Transform(uid).GridUid;
        return grid is not null && component.Grids.Contains(grid.Value);
    }

    public EntityUid InitializeNewStationMidRound(EntityUid gridId, List<EntProtoId> stationProtoIds, BecomesStationMidRoundComponent? comp = null)
    {
        if (!Resolve(gridId, ref comp)) return EntityUid.Invalid;
        if (stationProtoIds.Count == 0) stationProtoIds = [comp.DefaultBaseStationPrototype];
        //logic for if was initialized via BecomesStationMidRoundComponent
        ComponentRegistry? registry = null;
        registry = new ComponentRegistry();
        if (comp.AvailableJobs.Count > 0)
        {
            var jobs = new StationJobsComponent { SetupAvailableJobs = [] };
            foreach (var job in comp.AvailableJobs) jobs.SetupAvailableJobs.Add(job.Key, [job.Value, job.Value]);
            // from what I can tell the MappingDataNode doesn't actually need to have anything in it and from the looks of things seems to be primarily for setting up the entry in the first place.
            // no idea why it's needed in the constructor but oh well
            registry.Add("StationJobs", new EntityPrototype.ComponentRegistryEntry(jobs));
        }

        if (comp.EmergencyShuttleOverridePath is not null && comp.UseEmergencyShuttle) // no need to do this if its disabled anyway
        {
            var shuttle = new StationEmergencyShuttleComponent
            {
                EmergencyShuttlePath = new ResPath(comp.EmergencyShuttleOverridePath)
            };
            registry.Add("StationEmergencyShuttle", new EntityPrototype.ComponentRegistryEntry(shuttle));
        }

        var station = CreateCustomStation(stationProtoIds, MapCoordinates.Nullspace, registry);
        var data = EnsureComp<StationDataComponent>(station);
        RenameStation(station, MetaData(gridId).EntityName, false);
        var name = MetaData(station).EntityName;
        AddGridToStation(station, gridId, null, data, name);
        var ev = new StationPostInitEvent((station, data));
        RaiseLocalEvent(station, ref ev, true);
        if (!comp.AllowEvents)
            RemComp<StationEventEligibleComponent>(station);
        return station;
    }

    private EntityUid CreateCustomStation(List<EntProtoId> protoIds, MapCoordinates? coords, ComponentRegistry? registry)
    {
        var ent = EntityManager.CreateEntityUninitialized(null); // dummy entity

        var regTypes = registry is not null ? registry.Values.Select(c => _factory.GetRegistration(c.Component).Name).ToHashSet() : [];

        // do parents first
        foreach (var protoId in protoIds)
        {
            if (!_prototype.TryIndex(protoId, out var proto)) continue;
            foreach (var comp in proto.Components.Values.Where(comp => !HasComp(ent, comp.Component.GetType())))
            {
                if (regTypes.Contains(_factory.GetRegistration(comp.Component).Name)) continue;
                var newcomp = _factory.GetComponent(comp);
                AddComp(ent, newcomp);
            }
        }
        // now any of the extra overrides
        if (registry is not null)
        {
            foreach (var comp in registry.Values)
            {
                var newcomp = _factory.GetComponent(comp);
                AddComp(ent, newcomp);
            }
        }
        EntityManager.InitializeAndStartEntity(ent, coords!.Value.MapId);
        return ent;
    }
}
