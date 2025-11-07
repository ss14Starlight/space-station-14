using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Silicons.StationAi; // Starlight
using Robust.Client.UserInterface;
using Robust.Shared.Map; // Starlight
using Robust.Shared.Player; // Starlight

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
