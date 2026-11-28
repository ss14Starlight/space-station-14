// ReSharper disable CheckNamespace
using Content.Shared._Starlight.Medical.CrewMonitoring;
using Content.Shared.Medical.CrewMonitoring;

namespace Content.Client.Medical.CrewMonitoring;

public sealed partial class CrewMonitoringBoundUserInterface : BoundUserInterface
{
    private void UpdateFilter()
    {
        if (EntMan.TryGetComponent<CrewMonitoringFilterComponent>(Owner, out var component))
        {
            _menu!.PopulateTrackingFilters(component.AllowedTrackingImplants, component.ShownTrackingImplant);
        }
    }

    private void OnFilterSelected(string trackingImplant)
    {
        SendMessage(new CrewMonitoringUpdateFilterRequestMessage(trackingImplant));
    }
}
