using Content.Shared.Medical.CrewMonitoring;
using Robust.Client.UserInterface;
#region Starlight
using Content.Shared.Implants.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Player;
using System.Linq;
using Robust.Shared.Timing;
using Content.Shared.Medical.SuitSensors;
#endregion

namespace Content.Client.Medical.CrewMonitoring;

public sealed class CrewMonitoringBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!; // Starlight
    [Dependency] private readonly IGameTiming _gameTiming = default!; // Starlight

    [ViewVariables]
    private CrewMonitoringWindow? _menu;

    private TimeSpan _lastOpened = TimeSpan.Zero; // Starlight

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
        _lastOpened = _gameTiming.CurTime;
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
                bool awaitingData = st.Timestamp < _lastOpened; // Know whether we have real data or are viewing a cached state.
                bool serverOnline = _gameTiming.CurTime - st.LastUpdate < TimeSpan.FromSeconds(6); // After 6 seconds of radio silence, the server is presumed offline.
                _menu?.ShowSensors(awaitingData, serverOnline, st.Sensors, Owner, xform?.Coordinates);
                // Starlight end
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
