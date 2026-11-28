// ReSharper disable CheckNamespace

using Content.Shared._Starlight.Medical.CrewMonitoring;
using Content.Shared.Implants.Components;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;

namespace Content.Server.Medical.CrewMonitoring;
public sealed partial class CrewMonitoringConsoleSystem : EntitySystem
{
    private bool IsSensorValid(CrewMonitoringFilterComponent filter, SuitSensorStatus sensor)
    {
        var ClientEntity = GetEntity(sensor.SuitSensorUid);
        var TrackerPrototypeID = MetaData(ClientEntity).EntityPrototype!.ID;

        if (!TryComp<SubdermalImplantComponent>(ClientEntity, out var suitSensor) && (filter.ShownFactions.Count != 0 || !filter.ShownFactions.Contains(sensor.Faction)))
            return false;

        if (filter.ShownTrackingImplant == default && filter.AllowedTrackingImplants.Count != 0)
            filter.ShownTrackingImplant = "AllTrackers";

        if (filter.ShownTrackingImplant == default)
        {
            return true;
        }
        else if (filter.ShownTrackingImplant == TrackerPrototypeID)
        {
            return true;
        }
        else if (filter.ShownTrackingImplant == "AllTrackers")
        {
            foreach (string[] TrackerType in filter.AllowedTrackingImplants)
            {
                if (TrackerType[0] != TrackerPrototypeID)
                    continue;

                return true;
            }
        }

        return false;
    }

    private void OnFilterRequest(EntityUid uid, CrewMonitoringConsoleComponent component, ref CrewMonitoringUpdateFilterRequestMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
        {
            _sawmill.Warning($"Received crew monitor update filter request with no valid actor for console {uid}.");
            return;
        }

        if (!TryComp<CrewMonitoringFilterComponent>(uid, out var filter))
        {
            _sawmill.Warning($"Entity {Name(actor)} ({actor}) attempted to update filter on crew monitor {uid} without CrewMonitoringFilterComponent.");
            return;
        }

        if (args.TrackingImplant != "AllTrackers")
        {
            bool ValidRequest = false;

            foreach (string[] TrackerType in filter.AllowedTrackingImplants)
            {
                if (args.TrackingImplant == TrackerType[0])
                {
                    ValidRequest = true;
                    break;
                }
            }

            if (!ValidRequest)
            {
                _sawmill.Warning($"Entity {Name(actor)} ({actor}) attempted to update filter on crew monitor {uid} which is not in the AllowedTrackers.");
                return;
            }
        }

        filter.ShownTrackingImplant = args.TrackingImplant;
        Dirty(uid, filter);
        UpdateUserInterface(uid, component);
    }
}
