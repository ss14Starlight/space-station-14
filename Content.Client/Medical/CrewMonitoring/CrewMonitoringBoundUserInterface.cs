using Content.Shared.Medical.CrewMonitoring;
using Robust.Client.UserInterface;
using Content.Shared.Implants.Components; // Starlight
using Content.Shared.Silicons.StationAi; // Starlight
using Robust.Shared.Map; // Starlight
using Robust.Shared.Player; // Starlight
using System.Linq; // Starlight

namespace Content.Client.Medical.CrewMonitoring;

public sealed class CrewMonitoringBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!; // Starlight

    [ViewVariables]
    private CrewMonitoringWindow? _menu;

    public CrewMonitoringBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);     // Starlight
    }

    protected override void Open()
    {
        base.Open();

        // Starlight-start
        if (_menu != null)
            _menu.MapClicked -= OnMapClicked;
        // Starlight-end

        EntityUid? gridUid = null;
        var stationName = string.Empty;

        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
        {
            gridUid = xform.GridUid;

            if (EntMan.TryGetComponent<MetaDataComponent>(gridUid, out var metaData))
            {
                stationName = metaData.EntityName;
            }
        }

        _menu = this.CreateWindow<CrewMonitoringWindow>();
        _menu.Set(stationName, gridUid);
        _menu.MapClicked += OnMapClicked; // Starlight
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);


        switch (state)
        {
            case CrewMonitoringState st:
                EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
                // Starlight begin
                if (EntMan.TryGetComponent<CrewMonitoringFilterComponent>(Owner, out var filter))
                {
                    var filteredSensors = filter.ShownDepartments.Count == 0 ?
                        st.Sensors.ToList() // We ToList it to ensure we get a copy, for the off chance that someone sets AlwaysShowTrackingImplants without any ShownDepartments
                        : st.Sensors
                          .Where(sensor => sensor.JobDepartments.Intersect(filter.ShownDepartments).Count() != 0)
                          .ToList();

                    if (filter.AlwaysShowTrackingImplants)
                    {
                        foreach (var sensor in st.Sensors)
                        {
                            //get the client entity
                            var clientEntity = EntMan.GetEntity(sensor.SuitSensorUid);
                            if (EntMan.TryGetComponent<SubdermalImplantComponent>(clientEntity, out var suitSensor))
                            {
                                filteredSensors.Add(sensor);
                            }
                        }
                    }

                    if (filter.OnlyShowWoundedOrDead)
                    {
                        filteredSensors = filteredSensors
                            .Where(sensor =>
                                    (!sensor.IsAlive)
                                    || (sensor.DamagePercentage is not null && sensor.DamagePercentage > 0.5)).ToList();
                    }

                    filteredSensors = filteredSensors.Distinct().ToList();
                    _menu?.ShowSensors(filteredSensors, Owner, xform?.Coordinates);
                    break;
                }
                // We let it flow into the upstream code if there's no CrewMonitoringComponent
                // Starlight end
                _menu?.ShowSensors(st.Sensors, Owner, xform?.Coordinates);
                break;
        }
    }

    // Starlight-start
    private void OnMapClicked(EntityCoordinates coordinates)
    {
        var local = _playerManager.LocalEntity;

        if (local is null || !EntMan.HasComponent<StationAiHeldComponent>(local.Value))
            return;

        var netCoordinates = EntMan.GetNetCoordinates(coordinates);
        SendMessage(new CrewMonitoringWarpRequestMessage(netCoordinates));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_menu != null)
            {
                _menu.MapClicked -= OnMapClicked;
                _menu = null;
            }
        }

        base.Dispose(disposing);
    }
    // Starlight-end
}
